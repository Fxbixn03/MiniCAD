using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A circular arc. Angles are in radians, measured counter-clockwise from the positive
/// X axis; a negative <see cref="SweepAngle"/> describes a clockwise arc.
/// </summary>
public sealed class ArcEntity : Entity
{
    private double _radius;

    public ArcEntity(Point2D center, double radius, double startAngle, double sweepAngle)
    {
        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        SweepAngle = sweepAngle;
    }

    private ArcEntity(ArcEntity source) : base(source)
    {
        Center = source.Center;
        _radius = source._radius;
        StartAngle = source.StartAngle;
        SweepAngle = source.SweepAngle;
    }

    public Point2D Center { get; set; }

    public double Radius
    {
        get => _radius;
        set => _radius = Math.Abs(value);
    }

    public double StartAngle { get; set; }

    public double SweepAngle { get; set; }

    /// <summary>The world point on the arc at the given absolute angle (radians).</summary>
    public Point2D PointAt(double angle)
        => new(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));

    // A conservative full-circle bound. A tighter bound would consider the axis extents
    // that actually fall inside the sweep; the looser box is fine for hit-testing and fit.
    public override Rect2D Bounds => Rect2D.FromCenter(Center, Radius, Radius);

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Center;
            yield return PointAt(StartAngle);
            yield return PointAt(StartAngle + SweepAngle);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        if (Math.Abs(Center.DistanceTo(point) - Radius) > tolerance)
            return false;

        double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
        return ContainsAngle(angle);
    }

    private bool ContainsAngle(double angle)
    {
        if (SweepAngle >= 0)
            return GeometryMath.NormalizeAngle(angle - StartAngle) <= SweepAngle;

        return GeometryMath.NormalizeAngle(StartAngle - angle) <= -SweepAngle;
    }

    public override void Transform(in Matrix2D matrix)
    {
        Center = matrix.Transform(Center);
        Radius *= matrix.UniformScale;
        // Rotate the start angle by the transform's rotation component. Uniform scale and
        // rotation preserve the sweep; reflections are not supported.
        StartAngle += Math.Atan2(matrix.M12, matrix.M11);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawArc(Center, Radius, StartAngle, SweepAngle, stroke);

    public override IEntity Clone() => new ArcEntity(this);
}
