using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A smooth curve interpolating its fit points using a Catmull-Rom spline (the curve passes
/// through every point). It renders and hit-tests via a polyline tessellation and edits by
/// dragging the fit-point grips.
/// </summary>
public sealed class SplineEntity : Entity, IEditableEntity
{
    private const int StepsPerSpan = 16;

    private readonly List<Point2D> _points;

    public SplineEntity(IEnumerable<Point2D> points) => _points = new List<Point2D>(points);

    private SplineEntity(SplineEntity source) : base(source) => _points = new List<Point2D>(source._points);

    /// <summary>The fit points the curve passes through.</summary>
    public IReadOnlyList<Point2D> Points => _points;

    public void AddPoint(Point2D point) => _points.Add(point);

    /// <summary>The polyline approximation of the curve (the fit points themselves when fewer than 3).</summary>
    public List<Point2D> Tessellate()
    {
        if (_points.Count < 3)
            return new List<Point2D>(_points);

        var result = new List<Point2D>(_points.Count * StepsPerSpan + 1) { _points[0] };
        for (int i = 0; i < _points.Count - 1; i++)
        {
            Point2D p0 = _points[Math.Max(i - 1, 0)];
            Point2D p1 = _points[i];
            Point2D p2 = _points[i + 1];
            Point2D p3 = _points[Math.Min(i + 2, _points.Count - 1)];

            for (int step = 1; step <= StepsPerSpan; step++)
                result.Add(CatmullRom(p0, p1, p2, p3, (double)step / StepsPerSpan));
        }

        return result;
    }

    private static Point2D CatmullRom(Point2D p0, Point2D p1, Point2D p2, Point2D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
        double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
        return new Point2D(x, y);
    }

    public override Rect2D Bounds
    {
        get
        {
            List<Point2D> points = Tessellate();
            if (points.Count == 0)
                return new Rect2D(0, 0, 0, 0);

            Rect2D bounds = Rect2D.FromPoints(points[0], points[0]);
            for (int i = 1; i < points.Count; i++)
                bounds = bounds.Union(points[i]);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints => _points;

    public override bool HitTest(Point2D point, double tolerance)
    {
        List<Point2D> points = Tessellate();
        if (points.Count == 1)
            return point.DistanceTo(points[0]) <= tolerance;

        for (int i = 0; i < points.Count - 1; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, points[i], points[i + 1], out _) <= tolerance)
                return true;
        }

        return false;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        List<Point2D> points = Tessellate();
        if (points.Count == 1)
            return rect.Contains(points[0]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(points[i], points[i + 1], rect))
                return true;
        }

        return false;
    }

    public override void Transform(in Matrix2D matrix)
    {
        for (int i = 0; i < _points.Count; i++)
            _points[i] = matrix.Transform(_points[i]);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawPolyline(Tessellate(), closed: false, stroke);

    public override IEntity Clone() => new SplineEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips()
    {
        var grips = new List<Grip>(_points.Count);
        for (int i = 0; i < _points.Count; i++)
            grips.Add(new Grip(_points[i], GripKind.Vertex, i));
        return grips;
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index >= 0 && grip.Index < _points.Count)
            _points[grip.Index] = newPosition;
    }

    public object CaptureState() => new List<Point2D>(_points);

    public void RestoreState(object state)
    {
        _points.Clear();
        _points.AddRange((List<Point2D>)state);
    }
}
