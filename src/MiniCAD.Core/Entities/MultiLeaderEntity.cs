using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A multi-leader (#239): several arrowheads pointing at different features, all joined to a single
/// text label at a common <see cref="Landing"/> point. Each tip gets its own line and arrowhead;
/// the label is drawn once, like a <see cref="LeaderEntity"/> but with many leaders.
/// </summary>
public sealed class MultiLeaderEntity : Entity, IEditableEntity
{
    private const double BarbAngle = 0.40; // ~23° half-angle of the arrowhead

    private readonly List<Point2D> _tips;
    private double _textHeight;
    private double _arrowSize;

    public MultiLeaderEntity(Point2D landing, IEnumerable<Point2D> tips, string text,
        double textHeight = 12.0, double arrowSize = 12.0)
    {
        Landing = landing;
        _tips = tips?.ToList() ?? throw new ArgumentNullException(nameof(tips));
        if (_tips.Count < 1)
            throw new ArgumentException("A multi-leader needs at least one arrow tip.", nameof(tips));

        Text = text ?? string.Empty;
        TextHeight = textHeight;
        ArrowSize = arrowSize;
    }

    private MultiLeaderEntity(MultiLeaderEntity source) : base(source)
    {
        Landing = source.Landing;
        _tips = new List<Point2D>(source._tips);
        Text = source.Text;
        _textHeight = source._textHeight;
        _arrowSize = source._arrowSize;
    }

    /// <summary>The shared landing point where all leaders meet and the label sits.</summary>
    public Point2D Landing { get; set; }

    /// <summary>The arrow tips (one per leader line).</summary>
    public IReadOnlyList<Point2D> Tips => _tips;

    public string Text { get; set; }

    public double TextHeight
    {
        get => _textHeight;
        set => _textHeight = value <= 0.0 ? 1.0 : value;
    }

    public double ArrowSize
    {
        get => _arrowSize;
        set => _arrowSize = Math.Abs(value);
    }

    public override Rect2D Bounds
    {
        get
        {
            Rect2D bounds = Rect2D.FromPoints(Landing, _tips[0]);
            foreach (Point2D tip in _tips)
                bounds = bounds.Union(tip);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Landing;
            foreach (Point2D tip in _tips)
                yield return tip;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        foreach (Point2D tip in _tips)
        {
            if (GeometryMath.DistancePointToSegment(point, tip, Landing, out _) <= tolerance)
                return true;
        }

        if (Text.Length > 0 && BuildLabel().HitTest(point, tolerance))
            return true;

        return Landing.DistanceTo(point) <= tolerance;
    }

    public override bool IntersectsRect(Rect2D rect) => rect.Intersects(Bounds);

    public override void Transform(in Matrix2D matrix)
    {
        Landing = matrix.Transform(Landing);
        for (int i = 0; i < _tips.Count; i++)
            _tips[i] = matrix.Transform(_tips[i]);
        _textHeight *= matrix.UniformScale;
        _arrowSize *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        foreach (Point2D tip in _tips)
        {
            surface.DrawLine(tip, Landing, stroke);

            Vector2D back = tip.DistanceTo(Landing) > GeometryMath.Epsilon
                ? (tip - Landing).Normalized() * _arrowSize
                : new Vector2D(_arrowSize, 0);
            surface.DrawPolyline(new[] { tip + Rotate(back, BarbAngle), tip, tip + Rotate(back, -BarbAngle) }, closed: false, stroke);
        }

        if (Text.Length > 0)
            BuildLabel().Render(surface, stroke);
    }

    private TextEntity BuildLabel()
    {
        double averageTipX = _tips.Average(t => t.X);
        bool toRight = averageTipX <= Landing.X; // tips on the left → label on the right
        double gap = _textHeight * 0.3;
        var anchor = new Point2D(Landing.X + (toRight ? gap : -gap), Landing.Y);
        TextHAlign hAlign = toRight ? TextHAlign.Left : TextHAlign.Right;
        return new TextEntity(anchor, Text, _textHeight, 0.0, hAlign, TextVAlign.Middle);
    }

    private static Vector2D Rotate(Vector2D v, double angle)
    {
        double c = Math.Cos(angle);
        double s = Math.Sin(angle);
        return new Vector2D(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public override IEntity Clone() => new MultiLeaderEntity(this);

    public IReadOnlyList<Grip> GetGrips()
    {
        var grips = new List<Grip>(_tips.Count + 1) { new(Landing, GripKind.Vertex, 0) };
        for (int i = 0; i < _tips.Count; i++)
            grips.Add(new Grip(_tips[i], GripKind.Vertex, i + 1));
        return grips;
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
            Landing = newPosition;
        else if (grip.Index - 1 < _tips.Count)
            _tips[grip.Index - 1] = newPosition;
    }

    public object CaptureState() => (Landing, new List<Point2D>(_tips), Text, TextHeight, ArrowSize);

    public void RestoreState(object state)
    {
        var (landing, tips, text, textHeight, arrowSize) = ((Point2D, List<Point2D>, string, double, double))state;
        Landing = landing;
        _tips.Clear();
        _tips.AddRange(tips);
        Text = text;
        TextHeight = textHeight;
        ArrowSize = arrowSize;
    }
}
