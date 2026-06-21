using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A radius or diameter dimension for a circle/arc. <see cref="Center"/> is the circle centre,
/// <see cref="EdgePoint"/> a point on the circle (sets the radius and the arrow direction) and
/// <see cref="TextPoint"/> the leader/text location. With <see cref="IsDiameter"/> it measures
/// the diameter (⌀, arrows both sides through the centre); otherwise the radius (R).
/// </summary>
public sealed class RadialDimensionEntity : DimensionEntity, IEditableEntity
{
    public RadialDimensionEntity(Point2D center, Point2D edgePoint, Point2D textPoint, bool isDiameter = false)
    {
        Center = center;
        EdgePoint = edgePoint;
        TextPoint = textPoint;
        IsDiameter = isDiameter;
    }

    private RadialDimensionEntity(RadialDimensionEntity source) : base(source)
    {
        Center = source.Center;
        EdgePoint = source.EdgePoint;
        TextPoint = source.TextPoint;
        IsDiameter = source.IsDiameter;
    }

    public Point2D Center { get; set; }
    public Point2D EdgePoint { get; set; }
    public Point2D TextPoint { get; set; }
    public bool IsDiameter { get; set; }

    private double Radius => Center.DistanceTo(EdgePoint);

    /// <summary>The point diametrically opposite <see cref="EdgePoint"/> through the centre.</summary>
    private Point2D Opposite => new(2 * Center.X - EdgePoint.X, 2 * Center.Y - EdgePoint.Y);

    protected override string Prefix => IsDiameter ? "⌀" : "R";

    public override double MeasuredValue => IsDiameter ? 2 * Radius : Radius;

    public override Rect2D Bounds
    {
        get
        {
            return Rect2D.FromPoints(Center, EdgePoint).Union(TextPoint).Union(Opposite).Inflate(TextHeight);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Center;
            yield return EdgePoint;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        Point2D from = IsDiameter ? Opposite : Center;
        return GeometryMath.DistancePointToSegment(point, from, EdgePoint, out _) <= tolerance
            || GeometryMath.DistancePointToSegment(point, EdgePoint, TextPoint, out _) <= tolerance;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        Point2D from = IsDiameter ? Opposite : Center;
        return GeometryMath.SegmentIntersectsRect(from, EdgePoint, rect)
            || GeometryMath.SegmentIntersectsRect(EdgePoint, TextPoint, rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Center = matrix.Transform(Center);
        EdgePoint = matrix.Transform(EdgePoint);
        TextPoint = matrix.Transform(TextPoint);
        double scale = matrix.UniformScale;
        TextHeight *= scale;
        ArrowSize *= scale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        Vector2D dir = (EdgePoint - Center).Normalized();
        if (dir == Vector2D.Zero)
            return;

        DrawCenterMark(surface, stroke);

        if (IsDiameter)
        {
            Point2D opposite = Center - dir * Radius;
            surface.DrawLine(opposite, EdgePoint, stroke);
            DrawArrow(surface, EdgePoint, dir, stroke);
            DrawArrow(surface, opposite, -dir, stroke);
        }
        else
        {
            surface.DrawLine(Center, EdgePoint, stroke);
            DrawArrow(surface, EdgePoint, dir, stroke);
        }

        // Leader from the edge to the text, then the measurement text beside it.
        surface.DrawLine(EdgePoint, TextPoint, stroke);
        bool toRight = TextPoint.X >= EdgePoint.X;
        var textAnchor = new Point2D(TextPoint.X + (toRight ? TextHeight * 0.3 : -TextHeight * 0.3), TextPoint.Y);
        surface.DrawText(DisplayText, textAnchor, TextHeight, 0.0,
            toRight ? TextHAlign.Left : TextHAlign.Right, TextVAlign.Bottom, null, 1.0, stroke);
    }

    private void DrawCenterMark(IRenderSurface surface, in StrokeStyle stroke)
    {
        double s = Math.Max(ArrowSize, 1.0);
        surface.DrawLine(new Point2D(Center.X - s, Center.Y), new Point2D(Center.X + s, Center.Y), stroke);
        surface.DrawLine(new Point2D(Center.X, Center.Y - s), new Point2D(Center.X, Center.Y + s), stroke);
    }

    public override IEntity Clone() => new RadialDimensionEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Center, GripKind.Vertex, 0),
        new Grip(EdgePoint, GripKind.Radius, 1),
        new Grip(TextPoint, GripKind.Edge, 2),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: Center = newPosition; break;
            case 1: EdgePoint = newPosition; break;
            case 2: TextPoint = newPosition; break;
        }
    }

    public object CaptureState()
        => (Center, EdgePoint, TextPoint, IsDiameter, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride);

    public void RestoreState(object state)
        => (Center, EdgePoint, TextPoint, IsDiameter, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride)
            = ((Point2D, Point2D, Point2D, bool, double, double, double, double, int, string?))state;
}
