using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A connected sequence of segments, optionally closed into a polygon. Each segment can be a
/// straight line or a circular arc, controlled by a per-segment <em>bulge</em> (the tangent of a
/// quarter of the arc's included angle; 0 = straight, 1 = semicircle, sign = direction). The bulge
/// at index <c>i</c> shapes the segment leaving vertex <c>i</c>.
/// </summary>
public sealed class PolylineEntity : Entity, IEditableEntity
{
    private const double BulgeEpsilon = 1e-9;

    private readonly List<Point2D> _points;
    private readonly List<double> _bulges;

    public PolylineEntity(IEnumerable<Point2D> points, bool isClosed = false)
        : this(points, null, isClosed)
    {
    }

    public PolylineEntity(IEnumerable<Point2D> points, IEnumerable<double>? bulges, bool isClosed = false)
    {
        _points = new List<Point2D>(points);
        _bulges = NormalizeBulges(bulges, _points.Count);
        IsClosed = isClosed;
    }

    private PolylineEntity(PolylineEntity source) : base(source)
    {
        _points = new List<Point2D>(source._points);
        _bulges = new List<double>(source._bulges);
        IsClosed = source.IsClosed;
        Fill = source.Fill;
        SolidFill = source.SolidFill;
    }

    private static List<double> NormalizeBulges(IEnumerable<double>? bulges, int count)
    {
        var list = bulges is null ? new List<double>() : new List<double>(bulges);
        while (list.Count < count)
            list.Add(0.0);
        if (list.Count > count)
            list.RemoveRange(count, list.Count - count);
        return list;
    }

    public IReadOnlyList<Point2D> Points => _points;

    /// <summary>Per-segment bulges (one per vertex); index <c>i</c> shapes the segment from vertex <c>i</c>.</summary>
    public IReadOnlyList<double> Bulges => _bulges;

    public bool IsClosed { get; set; }

    /// <summary>
    /// An optional hatch pattern painted inside the region. Only meaningful for a closed
    /// polyline (a "Wand" or any closed object); ignored otherwise.
    /// </summary>
    public HatchPattern? Fill { get; set; }

    /// <summary>
    /// An optional solid/gradient area fill painted inside a closed region (separate from the
    /// hatch <see cref="Fill"/> and from the outline stroke); ignored when the polyline is open.
    /// </summary>
    public FillStyle? SolidFill { get; set; }

    /// <summary>Number of segments, accounting for the closing edge of a closed polyline.</summary>
    private int SegmentCount => IsClosed ? _points.Count : Math.Max(0, _points.Count - 1);

    private bool HasBulges
    {
        get
        {
            for (int i = 0; i < SegmentCount; i++)
                if (Math.Abs(_bulges[i]) > BulgeEpsilon)
                    return true;
            return false;
        }
    }

    public void AddPoint(Point2D point)
    {
        _points.Add(point);
        _bulges.Add(0.0);
    }

    /// <summary>Inserts a vertex into the middle of segment <paramref name="segmentIndex"/> (both halves straight).</summary>
    public void InsertVertex(int segmentIndex, Point2D point)
    {
        if (segmentIndex < 0 || segmentIndex >= SegmentCount)
            return;
        _points.Insert(segmentIndex + 1, point);
        _bulges[segmentIndex] = 0.0;
        _bulges.Insert(segmentIndex + 1, 0.0);
    }

    /// <summary>Removes the vertex at <paramref name="index"/>, straightening the segment it bridged.</summary>
    public void RemoveVertex(int index)
    {
        if (_points.Count <= 2 || index < 0 || index >= _points.Count)
            return;
        _points.RemoveAt(index);
        _bulges.RemoveAt(index);
        int prev = index - 1;
        if (prev >= 0 && prev < _bulges.Count)
            _bulges[prev] = 0.0;
    }

    /// <summary>Sets the bulge of segment <paramref name="segmentIndex"/> (0 = straight, ±1 = semicircle).</summary>
    public void SetBulge(int segmentIndex, double bulge)
    {
        if (segmentIndex >= 0 && segmentIndex < _bulges.Count)
            _bulges[segmentIndex] = bulge;
    }

    public double GetBulge(int segmentIndex)
        => segmentIndex >= 0 && segmentIndex < _bulges.Count ? _bulges[segmentIndex] : 0.0;

    /// <summary>The arc geometry of segment <paramref name="i"/>, or <c>null</c> when it is straight.</summary>
    public ArcEntity? SegmentArc(int i)
    {
        if (i < 0 || i >= SegmentCount)
            return null;
        double bulge = _bulges[i];
        if (Math.Abs(bulge) <= BulgeEpsilon)
            return null;
        return BulgeArc(_points[i], _points[(i + 1) % _points.Count], bulge);
    }

    /// <summary>Builds the arc of a bulged segment; always passes exactly through both endpoints.</summary>
    public static ArcEntity? BulgeArc(Point2D a, Point2D b, double bulge)
    {
        double theta = 4.0 * Math.Atan(bulge); // signed included angle
        Vector2D chord = b - a;
        double len = chord.Length;
        if (len <= GeometryMath.Epsilon || Math.Abs(Math.Sin(theta / 2)) <= GeometryMath.Epsilon)
            return null;

        double radius = len / (2.0 * Math.Sin(theta / 2));
        Vector2D dir = chord.Normalized();
        var normal = new Vector2D(-dir.Y, dir.X);
        double apothem = radius * Math.Cos(theta / 2);
        Point2D center = a.Lerp(b, 0.5) + normal * apothem;
        double startAngle = Math.Atan2(a.Y - center.Y, a.X - center.X);
        return new ArcEntity(center, Math.Abs(radius), startAngle, theta);
    }

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
            foreach (Point2D p in _points)
                bounds = bounds.Union(p);

            // Include the apex of each arc segment so the bulge isn't clipped out of the bounds.
            for (int i = 0; i < SegmentCount; i++)
                if (SegmentArc(i) is { } arc)
                    bounds = bounds.Union(arc.PointAt(arc.StartAngle + arc.SweepAngle / 2));

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

        for (int i = 0; i < SegmentCount; i++)
        {
            if (SegmentArc(i) is { } arc)
            {
                if (arc.HitTest(point, tolerance))
                    return true;
            }
            else if (GeometryMath.DistancePointToSegment(point, _points[i], _points[(i + 1) % _points.Count], out _) <= tolerance)
            {
                return true;
            }
        }

        // A filled region is also pickable anywhere inside it, not just on the outline.
        if (IsClosed && SolidFill is not null && _points.Count >= 3 && GeometryMath.PointInPolygon(_points, point))
            return true;

        return false;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        if (_points.Count == 0)
            return false;
        if (_points.Count == 1)
            return rect.Contains(_points[0]);

        for (int i = 0; i < SegmentCount; i++)
        {
            if (SegmentArc(i) is { } arc)
            {
                if (arc.IntersectsRect(rect))
                    return true;
            }
            else if (GeometryMath.SegmentIntersectsRect(_points[i], _points[(i + 1) % _points.Count], rect))
            {
                return true;
            }
        }

        return false;
    }

    public override void Transform(in Matrix2D matrix)
    {
        for (int i = 0; i < _points.Count; i++)
            _points[i] = matrix.Transform(_points[i]);

        // A reflection reverses arc orientation, so flip the bulges' sign.
        if (matrix.Determinant < 0)
            for (int i = 0; i < _bulges.Count; i++)
                _bulges[i] = -_bulges[i];
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        // Area fill first (under the hatch and outline), then hatch, then the outline on top.
        if (IsClosed && SolidFill is { } solid && _points.Count >= 3)
            surface.DrawFilledPolygon(_points, solid);

        if (IsClosed && Fill is { } fill && _points.Count >= 3)
        {
            StrokeStyle hatchStroke = fill.Stroke;
            foreach ((Point2D a, Point2D b) in HatchGeometry.Generate(_points, fill, surface.ModelScale))
                surface.DrawLine(a, b, hatchStroke);
        }

        if (!HasBulges)
        {
            surface.DrawPolyline(_points, IsClosed, stroke);
            return;
        }

        // Mixed straight/arc segments: draw each one in turn.
        for (int i = 0; i < SegmentCount; i++)
        {
            if (SegmentArc(i) is { } arc)
                surface.DrawArc(arc.Center, arc.Radius, arc.StartAngle, arc.SweepAngle, stroke);
            else
                surface.DrawLine(_points[i], _points[(i + 1) % _points.Count], stroke);
        }
    }

    public override IEntity Clone() => new PolylineEntity(this);

    // ----- Direct editing -----

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

    public object CaptureState() => (new List<Point2D>(_points), IsClosed, SolidFill, new List<double>(_bulges));

    public void RestoreState(object state)
    {
        var (points, closed, solidFill, bulges) = ((List<Point2D>, bool, FillStyle?, List<double>))state;
        _points.Clear();
        _points.AddRange(points);
        _bulges.Clear();
        _bulges.AddRange(NormalizeBulges(bulges, _points.Count));
        IsClosed = closed;
        SolidFill = solidFill;
    }
}
