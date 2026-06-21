using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Decomposes a composite entity into its independent constituent entities (#185, "Explode").
/// Polylines become individual line segments, blocks/parametric symbols become their contained
/// entities (transformed to world), and everything else that knows how to render itself —
/// dimensions, leaders, hatches — is broken into the primitives it draws (lines, arcs, text).
/// Atomic entities (a single line, circle, text, …) return an empty result: there is nothing to
/// explode. The caller is responsible for replacing the source with the result via undoable commands.
/// </summary>
public static class EntityExploder
{
    /// <summary>True if <paramref name="entity"/> decomposes into smaller parts.</summary>
    public static bool CanExplode(IEntity entity) => entity switch
    {
        PolylineEntity poly => poly.Points.Count >= 2,
        BlockReferenceEntity => true,
        ParametricSymbolEntity => true,
        LeaderEntity => true,
        DimensionEntity => true,
        _ => false,
    };

    /// <summary>Returns the constituent entities, or an empty list if the entity cannot be exploded.</summary>
    public static IReadOnlyList<IEntity> Explode(IEntity entity)
    {
        switch (entity)
        {
            case PolylineEntity poly:
                return ExplodePolyline(poly);

            case BlockReferenceEntity block:
                return Adopt(block.Explode(), block, keepOwnStyle: true);

            case ParametricSymbolEntity symbol:
                return Adopt(symbol.Explode(), symbol, keepOwnStyle: true);

            case LeaderEntity:
            case DimensionEntity:
                return Adopt(Capture(entity), entity, keepOwnStyle: false);

            default:
                return new List<IEntity>();
        }
    }

    private static List<IEntity> ExplodePolyline(PolylineEntity poly)
    {
        var parts = new List<IEntity>();
        IReadOnlyList<Point2D> pts = poly.Points;
        if (pts.Count < 2)
            return parts;

        int n = pts.Count;
        int segments = poly.IsClosed ? n : n - 1;
        for (int i = 0; i < segments; i++)
        {
            var line = new LineEntity(pts[i], pts[(i + 1) % n]);
            Inherit(line, poly);
            parts.Add(line);
        }

        // Dissolve a hatch fill into its constituent pattern lines.
        if (poly.IsClosed && poly.Fill is { } fill && n >= 3)
        {
            foreach ((Point2D a, Point2D b) in HatchGeometry.Generate(pts, fill, 1.0))
            {
                var line = new LineEntity(a, b);
                Inherit(line, poly);
                parts.Add(line);
            }
        }

        return parts;
    }

    /// <summary>Renders the entity into a recording surface, capturing the primitives as entities.</summary>
    private static List<IEntity> Capture(IEntity entity)
    {
        var surface = new CaptureSurface();
        entity.Render(surface, default);
        return surface.Entities;
    }

    /// <summary>Assigns the parts to the source's Teilbild (and layer/style/construction).</summary>
    private static List<IEntity> Adopt(IEnumerable<IEntity> parts, IEntity source, bool keepOwnStyle)
    {
        var result = new List<IEntity>();
        foreach (IEntity part in parts)
        {
            part.PartialDrawingId = source.PartialDrawingId;
            if (source.IsConstruction)
                part.IsConstruction = true;
            if (!keepOwnStyle)
            {
                part.LayerId = source.LayerId;
                part.StrokeOverride = source.StrokeOverride;
            }
            result.Add(part);
        }
        return result;
    }

    private static void Inherit(IEntity part, IEntity source)
    {
        part.LayerId = source.LayerId;
        part.PartialDrawingId = source.PartialDrawingId;
        part.StrokeOverride = source.StrokeOverride;
        part.IsConstruction = source.IsConstruction;
    }

    /// <summary>An <see cref="IRenderSurface"/> that turns drawing primitives into entities.</summary>
    private sealed class CaptureSurface : IRenderSurface
    {
        public List<IEntity> Entities { get; } = new();

        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) => Entities.Add(new LineEntity(a, b));

        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke)
        {
            if (points.Count < 2)
                return;
            if (points.Count == 2 && !closed)
                Entities.Add(new LineEntity(points[0], points[1]));
            else
                Entities.Add(new PolylineEntity(points, closed));
        }

        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke)
            => Entities.Add(new CircleEntity(center, radius));

        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke)
            => Entities.Add(new ArcEntity(center, radius, startAngle, sweepAngle));

        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign horizontalAlignment, TextVAlign verticalAlignment, string? fontFamily, double widthFactor,
            in StrokeStyle stroke)
            => Entities.Add(new TextEntity(position, text, height, rotation, horizontalAlignment, verticalAlignment)
            {
                FontFamily = fontFamily ?? string.Empty,
                WidthFactor = widthFactor,
            });

        public void DrawFilledPolygon(IReadOnlyList<Point2D> points, in FillStyle fill)
        {
            if (points.Count >= 3)
                Entities.Add(new PolylineEntity(points, isClosed: true));
        }
        // DrawImage: inherited no-op (raster underlays aren't decomposable into vector primitives).
    }
}
