using System.Globalization;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A graphical scale bar plan symbol (#213): a horizontal-ish bar from <see cref="Start"/> to
/// <see cref="End"/> divided into equal segments with alternating fill, labelled with the real
/// model length at each division boundary. Because drawing coordinates are model units, the labels
/// show the true spanned length directly.
/// </summary>
public sealed class ScaleBarEntity : Entity, IEditableEntity
{
    private int _divisions;
    private double _height;

    public ScaleBarEntity(Point2D start, Point2D end, int divisions = 4, double height = 20.0)
    {
        Start = start;
        End = end;
        Divisions = divisions;
        Height = height;
    }

    private ScaleBarEntity(ScaleBarEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
        _divisions = source._divisions;
        _height = source._height;
    }

    public Point2D Start { get; set; }

    public Point2D End { get; set; }

    /// <summary>Number of equal segments; always at least 1.</summary>
    public int Divisions
    {
        get => _divisions;
        set => _divisions = Math.Max(1, value);
    }

    /// <summary>Bar thickness in world units; always non-negative.</summary>
    public double Height
    {
        get => _height;
        set => _height = Math.Abs(value);
    }

    /// <summary>The full real length the bar spans, in model units.</summary>
    public double Length => Start.DistanceTo(End);

    private Vector2D Direction
    {
        get
        {
            Vector2D d = End - Start;
            return d.Length > GeometryMath.Epsilon ? d.Normalized() : new Vector2D(1, 0);
        }
    }

    private Vector2D Up => Direction.Perpendicular() * _height;

    public override Rect2D Bounds
    {
        get
        {
            Rect2D bounds = Rect2D.FromPoints(Start, End);
            return bounds.Union(Start + Up).Union(End + Up).Union(Start - Up).Union(End - Up);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            Vector2D dir = End - Start;
            for (int i = 0; i <= _divisions; i++)
                yield return Start + dir * ((double)i / _divisions);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => GeometryMath.DistancePointToSegment(point, Start, End, out _) <= Math.Max(tolerance, _height);

    public override bool IntersectsRect(Rect2D rect) => rect.Intersects(Bounds);

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
        Height *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        Vector2D full = End - Start;
        Vector2D up = Up;
        double step = Length / _divisions;

        for (int i = 0; i < _divisions; i++)
        {
            Point2D a = Start + full * ((double)i / _divisions);
            Point2D b = Start + full * ((double)(i + 1) / _divisions);
            var box = new[] { a, b, b + up, a + up };

            // Alternate filled/empty segments, the classic checker scale bar.
            if (i % 2 == 0)
                surface.DrawFilledPolygon(box, new FillStyle(stroke.Color, stroke.Color, isGradient: false, angleDegrees: 0));
            surface.DrawPolyline(box, closed: true, stroke);
        }

        double labelHeight = Math.Max(_height * 0.8, GeometryMath.Epsilon);
        double angle = Math.Atan2(full.Y, full.X);
        for (int i = 0; i <= _divisions; i++)
        {
            Point2D tick = Start + full * ((double)i / _divisions);
            string label = (step * i).ToString("0.##", CultureInfo.InvariantCulture);
            surface.DrawText(label, tick - up * 0.25, labelHeight, angle,
                TextHAlign.Center, TextVAlign.Top, null, 1.0, stroke);
        }
    }

    public override IEntity Clone() => new ScaleBarEntity(this);

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Start, GripKind.Vertex, 0),
        new Grip(End, GripKind.Vertex, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
            Start = newPosition;
        else
            End = newPosition;
    }

    public object CaptureState() => (Start, End, Divisions, Height);

    public void RestoreState(object state) => (Start, End, Divisions, Height) = ((Point2D, Point2D, int, double))state;
}
