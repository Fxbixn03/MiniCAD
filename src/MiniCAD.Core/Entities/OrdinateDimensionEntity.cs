using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A coordinate (ordinate) dimension: labels a point's X/Y relative to a reference origin
/// (the document's Nullpunkt at creation), drawn as a leader from the point to a stacked
/// "X …" / "Y …" label. Appearance follows the assigned DimStyle.
/// </summary>
public sealed class OrdinateDimensionEntity : DimensionEntity, IEditableEntity
{
    public OrdinateDimensionEntity(Point2D position, Point2D leaderEnd, Point2D origin)
    {
        Position = position;
        LeaderEnd = leaderEnd;
        Origin = origin;
    }

    private OrdinateDimensionEntity(OrdinateDimensionEntity source) : base(source)
    {
        Position = source.Position;
        LeaderEnd = source.LeaderEnd;
        Origin = source.Origin;
    }

    /// <summary>The labelled point.</summary>
    public Point2D Position { get; set; }

    /// <summary>The leader/text location.</summary>
    public Point2D LeaderEnd { get; set; }

    /// <summary>The reference origin (Nullpunkt) the coordinates are measured from.</summary>
    public Point2D Origin { get; set; }

    private double LocalX => Position.X - Origin.X;
    private double LocalY => Position.Y - Origin.Y;

    public override double MeasuredValue => LocalX;

    public override string DisplayText => TextOverride ?? $"X {FormatValue(LocalX)}  Y {FormatValue(LocalY)}";

    public override Rect2D Bounds
        => Rect2D.FromPoints(Position, LeaderEnd).Inflate(TextHeight * 2.5);

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            yield return LeaderEnd;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => GeometryMath.DistancePointToSegment(point, Position, LeaderEnd, out _) <= tolerance
        || LeaderEnd.DistanceTo(point) <= TextHeight * 2.0;

    public override bool IntersectsRect(Rect2D rect)
        => GeometryMath.SegmentIntersectsRect(Position, LeaderEnd, rect) || rect.Contains(LeaderEnd);

    public override void Transform(in Matrix2D matrix)
    {
        // Origin stays the document reference; the labelled point/leader move.
        Position = matrix.Transform(Position);
        LeaderEnd = matrix.Transform(LeaderEnd);
        TextHeight *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        surface.DrawLine(Position, LeaderEnd, stroke);

        bool toRight = LeaderEnd.X >= Position.X;
        TextHAlign align = toRight ? TextHAlign.Left : TextHAlign.Right;
        double gap = TextHeight * 0.3;
        double line = TextHeight * 1.2;
        var anchor = new Point2D(LeaderEnd.X + (toRight ? gap : -gap), LeaderEnd.Y);

        surface.DrawText($"X {FormatValue(LocalX)}", new Point2D(anchor.X, anchor.Y + line * 0.5),
            TextHeight, 0.0, align, TextVAlign.Middle, null, 1.0, stroke);
        surface.DrawText($"Y {FormatValue(LocalY)}", new Point2D(anchor.X, anchor.Y - line * 0.5),
            TextHeight, 0.0, align, TextVAlign.Middle, null, 1.0, stroke);
    }

    public override IEntity Clone() => new OrdinateDimensionEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Position, GripKind.Vertex, 0),
        new Grip(LeaderEnd, GripKind.Edge, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
            Position = newPosition;
        else
            LeaderEnd = newPosition;
    }

    public object CaptureState()
        => (Position, LeaderEnd, Origin, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride);

    public void RestoreState(object state)
        => (Position, LeaderEnd, Origin, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride)
            = ((Point2D, Point2D, Point2D, double, double, double, double, int, string?))state;
}
