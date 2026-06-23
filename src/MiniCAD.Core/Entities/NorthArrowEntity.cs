using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A north-arrow plan symbol (#213): an arrow with an "N" label that points in a configurable
/// direction. <see cref="Direction"/> is the world angle the arrow points (radians, CCW from +X);
/// the default π/2 points up (north up). <see cref="Size"/> is the arrow length in world units.
/// </summary>
public sealed class NorthArrowEntity : Entity, IEditableEntity
{
    private double _size;

    public NorthArrowEntity(Point2D position, double size = 60.0, double direction = Math.PI / 2.0)
    {
        Position = position;
        Size = size;
        Direction = direction;
    }

    private NorthArrowEntity(NorthArrowEntity source) : base(source)
    {
        Position = source.Position;
        _size = source._size;
        Direction = source.Direction;
    }

    /// <summary>The insertion point (arrow center).</summary>
    public Point2D Position { get; set; }

    /// <summary>Arrow length in world units; always non-negative.</summary>
    public double Size
    {
        get => _size;
        set => _size = Math.Abs(value);
    }

    /// <summary>World direction the arrow points, in radians CCW from +X (π/2 = north up).</summary>
    public double Direction { get; set; }

    /// <summary>Maps a unit-shape point (built along +X) to world space.</summary>
    private Point2D Local(double x, double y)
    {
        double c = Math.Cos(Direction);
        double s = Math.Sin(Direction);
        return new Point2D(
            Position.X + _size * (x * c - y * s),
            Position.Y + _size * (x * s + y * c));
    }

    public override Rect2D Bounds
    {
        get
        {
            Rect2D bounds = Rect2D.FromPoints(Local(-0.5, 0), Local(0.85, 0));
            return bounds
                .Union(Local(0.3, 0.15))
                .Union(Local(0.3, -0.15));
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            yield return Local(0.5, 0);  // tip
            yield return Local(-0.5, 0); // tail
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        Point2D tail = Local(-0.5, 0);
        Point2D tip = Local(0.5, 0);
        return GeometryMath.DistancePointToSegment(point, tail, tip, out _) <= Math.Max(tolerance, _size * 0.1);
    }

    public override bool IntersectsRect(Rect2D rect) => rect.Intersects(Bounds);

    public override void Transform(in Matrix2D matrix)
    {
        // Derive the new direction from a transformed unit vector along the arrow.
        var unit = new Vector2D(Math.Cos(Direction), Math.Sin(Direction));
        Vector2D transformed = matrix.TransformVector(unit);

        Position = matrix.Transform(Position);
        if (transformed.Length > GeometryMath.Epsilon)
            Direction = Math.Atan2(transformed.Y, transformed.X);
        Size *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        Point2D tail = Local(-0.5, 0);
        Point2D tip = Local(0.5, 0);
        surface.DrawLine(tail, tip, stroke);
        surface.DrawLine(tip, Local(0.3, 0.15), stroke);
        surface.DrawLine(tip, Local(0.3, -0.15), stroke);

        surface.DrawText("N", Local(0.78, 0), _size * 0.3, Direction - Math.PI / 2.0,
            TextHAlign.Center, TextVAlign.Middle, null, 1.0, stroke);
    }

    public override IEntity Clone() => new NorthArrowEntity(this);

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Position, GripKind.Vertex, 0),
        new Grip(Local(0.5, 0), GripKind.Vertex, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
        {
            Position = newPosition;
        }
        else
        {
            Vector2D d = newPosition - Position;
            if (d.Length > GeometryMath.Epsilon)
            {
                Direction = Math.Atan2(d.Y, d.X);
                Size = d.Length * 2.0;
            }
        }
    }

    public object CaptureState() => (Position, Size, Direction);

    public void RestoreState(object state) => (Position, Size, Direction) = ((Point2D, double, double))state;
}
