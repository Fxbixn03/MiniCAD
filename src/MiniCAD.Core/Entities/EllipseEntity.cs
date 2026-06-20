using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An ellipse or elliptical arc, defined by a centre, two semi-axes and a rotation. A full
/// ellipse has <see cref="SweepAngle"/> = 2π; a partial one carries a start angle and sweep
/// (in the ellipse's own parameter space). It renders and hit-tests via a polyline tessellation,
/// so no new render primitive is needed.
/// </summary>
public sealed class EllipseEntity : Entity, IEditableEntity
{
    private const int Segments = 72;
    private const double FullSweep = Math.PI * 2.0;

    private double _radiusX;
    private double _radiusY;

    public EllipseEntity(Point2D center, double radiusX, double radiusY, double rotation = 0.0,
        double startAngle = 0.0, double sweepAngle = FullSweep)
    {
        Center = center;
        RadiusX = radiusX;
        RadiusY = radiusY;
        Rotation = rotation;
        StartAngle = startAngle;
        SweepAngle = sweepAngle;
    }

    private EllipseEntity(EllipseEntity source) : base(source)
    {
        Center = source.Center;
        _radiusX = source._radiusX;
        _radiusY = source._radiusY;
        Rotation = source.Rotation;
        StartAngle = source.StartAngle;
        SweepAngle = source.SweepAngle;
    }

    public Point2D Center { get; set; }

    /// <summary>Semi-axis along the rotated X direction; always non-negative.</summary>
    public double RadiusX
    {
        get => _radiusX;
        set => _radiusX = Math.Abs(value);
    }

    /// <summary>Semi-axis along the rotated Y direction; always non-negative.</summary>
    public double RadiusY
    {
        get => _radiusY;
        set => _radiusY = Math.Abs(value);
    }

    /// <summary>Rotation of the major axis, radians CCW from the world X axis.</summary>
    public double Rotation { get; set; }

    public double StartAngle { get; set; }

    public double SweepAngle { get; set; }

    public bool IsFull => Math.Abs(SweepAngle) >= FullSweep - GeometryMath.Epsilon;

    /// <summary>The world point at parameter <paramref name="angle"/> (the ellipse's own angle).</summary>
    public Point2D PointAt(double angle)
    {
        double lx = RadiusX * Math.Cos(angle);
        double ly = RadiusY * Math.Sin(angle);
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);
        return new Point2D(Center.X + lx * cos - ly * sin, Center.Y + lx * sin + ly * cos);
    }

    private List<Point2D> Tessellate()
    {
        bool full = IsFull;
        int count = full ? Segments : Segments + 1;
        var points = new List<Point2D>(count);
        for (int i = 0; i < count; i++)
            points.Add(PointAt(StartAngle + SweepAngle * i / Segments));
        return points;
    }

    public override Rect2D Bounds
    {
        get
        {
            List<Point2D> points = Tessellate();
            Rect2D bounds = Rect2D.FromPoints(points[0], points[0]);
            for (int i = 1; i < points.Count; i++)
                bounds = bounds.Union(points[i]);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Center;
            yield return PointAt(0);
            yield return PointAt(Math.PI / 2);
            yield return PointAt(Math.PI);
            yield return PointAt(3 * Math.PI / 2);
            if (!IsFull)
            {
                yield return PointAt(StartAngle);
                yield return PointAt(StartAngle + SweepAngle);
            }
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        List<Point2D> points = Tessellate();
        for (int i = 0; i < points.Count - 1; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, points[i], points[i + 1], out _) <= tolerance)
                return true;
        }

        return IsFull && GeometryMath.DistancePointToSegment(point, points[^1], points[0], out _) <= tolerance;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        List<Point2D> points = Tessellate();
        for (int i = 0; i < points.Count - 1; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(points[i], points[i + 1], rect))
                return true;
        }

        return IsFull && GeometryMath.SegmentIntersectsRect(points[^1], points[0], rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        // Conformal transforms (translate/rotate/uniform-scale, incl. reflection) are exact;
        // a non-uniform scale is approximated by treating the average scale as uniform.
        var axis = new Vector2D(Math.Cos(Rotation), Math.Sin(Rotation));
        Vector2D transformedAxis = matrix.TransformVector(axis);

        Center = matrix.Transform(Center);
        RadiusX *= matrix.UniformScale;
        RadiusY *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
        if (matrix.Determinant < 0)
            SweepAngle = -SweepAngle;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawPolyline(Tessellate(), IsFull, stroke);

    public override IEntity Clone() => new EllipseEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Center, GripKind.Vertex, 0),
        new Grip(PointAt(0), GripKind.Radius, 1),          // major axis endpoint
        new Grip(PointAt(Math.PI / 2), GripKind.Radius, 2), // minor axis endpoint
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0:
                Center = newPosition;
                break;
            case 1: // major endpoint: sets RadiusX and the rotation
                RadiusX = Center.DistanceTo(newPosition);
                Rotation = Math.Atan2(newPosition.Y - Center.Y, newPosition.X - Center.X);
                break;
            case 2: // minor endpoint: sets RadiusY (perpendicular distance to the major axis)
                RadiusY = Center.DistanceTo(newPosition);
                break;
        }
    }

    public object CaptureState() => (Center, RadiusX, RadiusY, Rotation, StartAngle, SweepAngle);

    public void RestoreState(object state)
        => (Center, RadiusX, RadiusY, Rotation, StartAngle, SweepAngle) = ((Point2D, double, double, double, double, double))state;
}
