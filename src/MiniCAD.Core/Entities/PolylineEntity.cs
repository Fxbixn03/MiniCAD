using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>A connected sequence of line segments, optionally closed into a polygon.</summary>
public sealed class PolylineEntity : Entity, IEditableEntity
{
    private readonly List<Point2D> _points;

    public PolylineEntity(IEnumerable<Point2D> points, bool isClosed = false)
    {
        _points = new List<Point2D>(points);
        IsClosed = isClosed;
    }

    private PolylineEntity(PolylineEntity source) : base(source)
    {
        _points = new List<Point2D>(source._points);
        IsClosed = source.IsClosed;
        Fill = source.Fill;
    }

    public IReadOnlyList<Point2D> Points => _points;

    public bool IsClosed { get; set; }

    /// <summary>
    /// An optional hatch pattern painted inside the region. Only meaningful for a closed
    /// polyline (a "Wand" or any closed object); ignored otherwise.
    /// </summary>
    public HatchPattern? Fill { get; set; }

    public void AddPoint(Point2D point) => _points.Add(point);

    /// <summary>Builds an axis-aligned rectangle as a closed four-point polyline.</summary>
    public static PolylineEntity Rectangle(Point2D min, Point2D max) => new(
        new[]
        {
            new Point2D(min.X, min.Y),
            new Point2D(max.X, min.Y),
            new Point2D(max.X, max.Y),
            new Point2D(min.X, max.Y),
        },
        isClosed: true);

    public override Rect2D Bounds
    {
        get
        {
            if (_points.Count == 0)
                return new Rect2D(0, 0, 0, 0);

            Rect2D bounds = Rect2D.FromPoints(_points[0], _points[0]);
            for (int i = 1; i < _points.Count; i++)
                bounds = bounds.Union(_points[i]);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints => _points;

    public override bool HitTest(Point2D point, double tolerance)
    {
        if (_points.Count == 0)
            return false;
        if (_points.Count == 1)
            return point.DistanceTo(_points[0]) <= tolerance;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, _points[i], _points[i + 1], out _) <= tolerance)
                return true;
        }

        if (IsClosed && GeometryMath.DistancePointToSegment(point, _points[^1], _points[0], out _) <= tolerance)
            return true;

        return false;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        if (_points.Count == 0)
            return false;
        if (_points.Count == 1)
            return rect.Contains(_points[0]);

        for (int i = 0; i < _points.Count - 1; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(_points[i], _points[i + 1], rect))
                return true;
        }

        return IsClosed && GeometryMath.SegmentIntersectsRect(_points[^1], _points[0], rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        for (int i = 0; i < _points.Count; i++)
            _points[i] = matrix.Transform(_points[i]);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        if (IsClosed && Fill is { } fill && _points.Count >= 3)
        {
            StrokeStyle hatchStroke = fill.Stroke;
            foreach ((Point2D a, Point2D b) in HatchGeometry.Generate(_points, fill))
                surface.DrawLine(a, b, hatchStroke);
        }

        surface.DrawPolyline(_points, IsClosed, stroke);
    }

    public override IEntity Clone() => new PolylineEntity(this);

    // ----- Direct editing -----

    /// <summary>Number of segments, accounting for the closing edge of a closed polyline.</summary>
    private int SegmentCount => IsClosed ? _points.Count : _points.Count - 1;

    /// <summary>
    /// One vertex grip per point plus one edge grip per segment. Vertex grips use indices
    /// <c>0..N-1</c>; edge grip <c>i</c> (index <c>N + i</c>) slides the segment that starts at
    /// vertex <c>i</c>, so dragging one side of a rectangle keeps the opposite side in place.
    /// </summary>
    public IReadOnlyList<Grip> GetGrips()
    {
        var grips = new List<Grip>(_points.Count + SegmentCount);
        for (int i = 0; i < _points.Count; i++)
            grips.Add(new Grip(_points[i], GripKind.Vertex, i));

        for (int i = 0; i < SegmentCount; i++)
        {
            Point2D a = _points[i];
            Point2D b = _points[(i + 1) % _points.Count];
            grips.Add(new Grip(a.Lerp(b, 0.5), GripKind.Edge, _points.Count + i));
        }

        return grips;
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index < _points.Count)
        {
            _points[grip.Index] = newPosition;
            return;
        }

        int segment = grip.Index - _points.Count;
        if (segment < 0 || segment >= SegmentCount)
            return;

        int i = segment;
        int j = (segment + 1) % _points.Count;
        Vector2D delta = newPosition - _points[i].Lerp(_points[j], 0.5);
        _points[i] += delta;
        _points[j] += delta;
    }

    public object CaptureState() => (new List<Point2D>(_points), IsClosed);

    public void RestoreState(object state)
    {
        var (points, closed) = ((List<Point2D>, bool))state;
        _points.Clear();
        _points.AddRange(points);
        IsClosed = closed;
    }
}
