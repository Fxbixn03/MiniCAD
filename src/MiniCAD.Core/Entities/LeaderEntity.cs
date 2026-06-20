using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A leader / callout: an arrowhead at the first vertex, a multi-segment line through the
/// remaining vertices, and a horizontal text label coupled to the final (landing) vertex.
/// The first point is the arrow tip (it points at the annotated feature); the last point is
/// where the text sits. The label is rendered through a transient <see cref="TextEntity"/>,
/// so it automatically benefits from the shared text rendering.
/// </summary>
public sealed class LeaderEntity : Entity, IEditableEntity
{
    private const double BarbAngle = 0.40; // ~23° half-angle of the arrowhead

    private readonly List<Point2D> _points;
    private double _textHeight;
    private double _arrowSize;

    public LeaderEntity(IEnumerable<Point2D> points, string text, double textHeight = 12.0, double arrowSize = 12.0)
    {
        _points = points?.ToList() ?? throw new ArgumentNullException(nameof(points));
        if (_points.Count < 2)
            throw new ArgumentException("A leader needs at least two points.", nameof(points));

        Text = text ?? string.Empty;
        TextHeight = textHeight;
        ArrowSize = arrowSize;
    }

    private LeaderEntity(LeaderEntity source) : base(source)
    {
        _points = new List<Point2D>(source._points);
        Text = source.Text;
        _textHeight = source._textHeight;
        _arrowSize = source._arrowSize;
    }

    /// <summary>The leader vertices: [0] is the arrow tip, [^1] the text landing point.</summary>
    public IReadOnlyList<Point2D> Points => _points;

    public string Text { get; set; }

    /// <summary>Cap height of the label, in world units; always positive.</summary>
    public double TextHeight
    {
        get => _textHeight;
        set => _textHeight = value <= 0.0 ? 1.0 : value;
    }

    /// <summary>Length of the arrowhead barbs, in world units; always non-negative.</summary>
    public double ArrowSize
    {
        get => _arrowSize;
        set => _arrowSize = Math.Abs(value);
    }

    /// <summary>The label, anchored beside the landing point and rendered as horizontal text.</summary>
    private TextEntity BuildLabel()
    {
        Point2D landing = _points[^1];
        bool toRight = landing.X >= _points[^2].X;
        double gap = _textHeight * 0.3;
        var anchor = new Point2D(landing.X + (toRight ? gap : -gap), landing.Y);
        TextHAlign hAlign = toRight ? TextHAlign.Left : TextHAlign.Right;
        return new TextEntity(anchor, Text, _textHeight, 0.0, hAlign, TextVAlign.Bottom);
    }

    /// <summary>The arrowhead barb endpoints at the tip, pointing back along the first segment.</summary>
    private (Point2D Barb1, Point2D Barb2) ArrowBarbs()
    {
        Point2D tip = _points[0];
        Vector2D back = (tip - _points[1]).Normalized() * _arrowSize;
        return (tip + Rotate(back, BarbAngle), tip + Rotate(back, -BarbAngle));
    }

    private static Vector2D Rotate(Vector2D v, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new Vector2D(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    public override Rect2D Bounds
    {
        get
        {
            Rect2D bounds = Rect2D.FromPoints(_points[0], _points[0]);
            foreach (Point2D p in _points)
                bounds = bounds.Union(p);
            bounds = bounds.Union(BuildLabel().Bounds);
            (Point2D b1, Point2D b2) = ArrowBarbs();
            return bounds.Union(b1).Union(b2);
        }
    }

    public override IEnumerable<Point2D> SnapPoints => _points;

    public override bool HitTest(Point2D point, double tolerance)
    {
        for (int i = 0; i < _points.Count - 1; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, _points[i], _points[i + 1], out _) <= tolerance)
                return true;
        }

        return BuildLabel().HitTest(point, tolerance);
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        for (int i = 0; i < _points.Count - 1; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(_points[i], _points[i + 1], rect))
                return true;
        }

        return BuildLabel().IntersectsRect(rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        for (int i = 0; i < _points.Count; i++)
            _points[i] = matrix.Transform(_points[i]);

        TextHeight *= matrix.UniformScale;
        ArrowSize *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        surface.DrawPolyline(_points, closed: false, stroke);

        (Point2D b1, Point2D b2) = ArrowBarbs();
        surface.DrawPolyline(new[] { b1, _points[0], b2 }, closed: false, stroke);

        if (Text.Length > 0)
            BuildLabel().Render(surface, stroke);
    }

    public override IEntity Clone() => new LeaderEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips()
    {
        var grips = new Grip[_points.Count];
        for (int i = 0; i < _points.Count; i++)
            grips[i] = new Grip(_points[i], GripKind.Vertex, i);
        return grips;
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index >= 0 && grip.Index < _points.Count)
            _points[grip.Index] = newPosition;
    }

    public object CaptureState() => (new List<Point2D>(_points), Text, TextHeight, ArrowSize);

    public void RestoreState(object state)
    {
        (List<Point2D> points, string text, double textHeight, double arrowSize) =
            ((List<Point2D>, string, double, double))state;
        _points.Clear();
        _points.AddRange(points);
        Text = text;
        TextHeight = textHeight;
        ArrowSize = arrowSize;
    }
}
