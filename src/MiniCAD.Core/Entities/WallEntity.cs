using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An architectural wall (#73): a straight axis (centerline) with a thickness, plus the height
/// data that drives its 3D solid — the base elevation (Z of the underside) and the wall height.
/// It draws in plan as its footprint outline (Wandlaibung) and is the 2D source from which the live
/// 3D model is derived (see <c>WallModelBuilder</c>). Endpoints, thickness, height and base
/// elevation are all editable (tool, grips, properties panel) and persisted.
/// </summary>
public sealed class WallEntity : Entity, IEditableEntity
{
    private double _thickness;
    private double _height;

    public WallEntity(Point2D start, Point2D end, double thickness = 240.0, double height = 2500.0,
        double baseElevation = 0.0)
    {
        Start = start;
        End = end;
        Thickness = thickness;
        Height = height;
        BaseElevation = baseElevation;
    }

    private WallEntity(WallEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
        _thickness = source._thickness;
        _height = source._height;
        BaseElevation = source.BaseElevation;
    }

    /// <summary>Axis (centerline) start point in world XY.</summary>
    public Point2D Start { get; set; }

    /// <summary>Axis (centerline) end point in world XY.</summary>
    public Point2D End { get; set; }

    /// <summary>Wall thickness in world units; always positive.</summary>
    public double Thickness
    {
        get => _thickness;
        set => _thickness = value <= 0 ? 1.0 : value;
    }

    /// <summary>Wall height (Z extent) in world units; always positive.</summary>
    public double Height
    {
        get => _height;
        set => _height = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z of the wall's underside (e.g. 0 = floor level, 10 = starts at Z=10).</summary>
    public double BaseElevation { get; set; }

    /// <summary>Z of the wall's top.</summary>
    public double TopElevation => BaseElevation + _height;

    /// <summary>Axis length in plan.</summary>
    public double Length => Start.DistanceTo(End);

    /// <summary>Unit vector perpendicular to the axis (for offsetting to the wall faces).</summary>
    private Vector2D Normal
    {
        get
        {
            Vector2D dir = End - Start;
            if (dir.Length <= GeometryMath.Epsilon)
                return new Vector2D(0, 1);
            dir = dir.Normalized();
            return new Vector2D(-dir.Y, dir.X);
        }
    }

    /// <summary>The four footprint corners in order (start-left, end-left, end-right, start-right).</summary>
    public IReadOnlyList<Point2D> Footprint()
    {
        Vector2D half = Normal * (_thickness / 2);
        return new[]
        {
            Start + half,
            End + half,
            End - half,
            Start - half,
        };
    }

    public override Rect2D Bounds
    {
        get
        {
            IReadOnlyList<Point2D> corners = Footprint();
            Rect2D bounds = Rect2D.FromPoints(corners[0], corners[0]);
            foreach (Point2D c in corners)
                bounds = bounds.Union(c);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Start;
            yield return End;
            yield return Start.Lerp(End, 0.5);
            foreach (Point2D c in Footprint())
                yield return c;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        // On any footprint edge…
        for (int i = 0; i < corners.Count; i++)
        {
            Point2D a = corners[i], b = corners[(i + 1) % corners.Count];
            if (GeometryMath.DistancePointToSegment(point, a, b, out _) <= tolerance)
                return true;
        }
        // …or anywhere inside the wall body (walls are solid in plan).
        return GeometryMath.PointInPolygon(corners, point);
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        for (int i = 0; i < corners.Count; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(corners[i], corners[(i + 1) % corners.Count], rect))
                return true;
        }
        return rect.Contains(corners[0]);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
        Thickness *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        surface.DrawPolyline(corners, closed: true, stroke);
    }

    public override IEntity Clone() => new WallEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Start, GripKind.Vertex, 0),
        new Grip(End, GripKind.Vertex, 1),
        new Grip(Start.Lerp(End, 0.5), GripKind.Edge, 2),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: Start = newPosition; break;
            case 1: End = newPosition; break;
            case 2:
                Vector2D delta = newPosition - Start.Lerp(End, 0.5);
                Start += delta;
                End += delta;
                break;
        }
    }

    public object CaptureState() => (Start, End, _thickness, _height, BaseElevation);

    public void RestoreState(object state)
        => (Start, End, _thickness, _height, BaseElevation) = ((Point2D, Point2D, double, double, double))state;
}
