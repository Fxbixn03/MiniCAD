using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>A full circle defined by a center and radius.</summary>
public sealed class CircleEntity : Entity, IEditableEntity
{
    private double _radius;

    public CircleEntity(Point2D center, double radius)
    {
        Center = center;
        Radius = radius;
    }

    private CircleEntity(CircleEntity source) : base(source)
    {
        Center = source.Center;
        _radius = source._radius;
    }

    public Point2D Center { get; set; }

    /// <summary>The radius; always non-negative.</summary>
    public double Radius
    {
        get => _radius;
        set => _radius = Math.Abs(value);
    }

    public override Rect2D Bounds => Rect2D.FromCenter(Center, Radius, Radius);

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Center;
            yield return new Point2D(Center.X + Radius, Center.Y); // quadrant points
            yield return new Point2D(Center.X - Radius, Center.Y);
            yield return new Point2D(Center.X, Center.Y + Radius);
            yield return new Point2D(Center.X, Center.Y - Radius);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => Math.Abs(Center.DistanceTo(point) - Radius) <= tolerance;

    public override bool IntersectsRect(Rect2D rect)
    {
        // The outline meets the rect iff some rect point is exactly the radius away — i.e. the
        // radius falls between the nearest and farthest distances from the centre to the box.
        // (A box fully inside the disk, or a disk fully inside the box, are both "crossing".)
        return rect.DistanceTo(Center) <= Radius && Radius <= rect.MaxCornerDistance(Center);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Center = matrix.Transform(Center);
        Radius *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawCircle(Center, Radius, stroke);

    public override IEntity Clone() => new CircleEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Center, GripKind.Vertex, 0),
        new Grip(new Point2D(Center.X + Radius, Center.Y), GripKind.Radius, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
            Center = newPosition;
        else
            Radius = Center.DistanceTo(newPosition);
    }

    public object CaptureState() => (Center, Radius);

    public void RestoreState(object state)
    {
        (Center, double radius) = ((Point2D, double))state;
        Radius = radius;
    }
}
