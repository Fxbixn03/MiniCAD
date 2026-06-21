using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A level/elevation mark (Höhenkote): a filled triangle whose apex sits on
/// <see cref="Position"/> and a height value shown above it (e.g. "+2.75", "±0.00"). The value
/// is stored directly (until a real Z height exists); appearance follows the assigned DimStyle.
/// </summary>
public sealed class ElevationDimensionEntity : DimensionEntity, IEditableEntity
{
    public ElevationDimensionEntity(Point2D position, double zValue)
    {
        Position = position;
        ZValue = zValue;
    }

    private ElevationDimensionEntity(ElevationDimensionEntity source) : base(source)
    {
        Position = source.Position;
        ZValue = source.ZValue;
    }

    /// <summary>The marked point (the triangle apex).</summary>
    public Point2D Position { get; set; }

    /// <summary>The height value shown by the mark.</summary>
    public double ZValue { get; set; }

    public override double MeasuredValue => ZValue;

    public override string DisplayText
    {
        get
        {
            if (TextOverride is { } over)
                return over;

            string body = FormatValue(Math.Abs(ZValue));
            string sign = ZValue > GeometryMath.Epsilon ? "+" : ZValue < -GeometryMath.Epsilon ? "-" : "±";
            return sign + body;
        }
    }

    private double Size => Math.Max(TextHeight, 1.0);

    private Point2D[] Triangle()
    {
        double h = Size;
        return new[]
        {
            Position,                                       // apex on the point
            new Point2D(Position.X - h * 0.5, Position.Y + h),
            new Point2D(Position.X + h * 0.5, Position.Y + h),
        };
    }

    public override Rect2D Bounds
    {
        get
        {
            Point2D[] t = Triangle();
            Rect2D bounds = Rect2D.FromPoints(t[1], t[2]).Union(t[0]);
            return bounds.Inflate(TextHeight * 1.5);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get { yield return Position; }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => Position.DistanceTo(point) <= Math.Max(tolerance, Size)
        || GeometryMath.PointInPolygon(Triangle(), point);

    public override bool IntersectsRect(Rect2D rect)
    {
        Point2D[] t = Triangle();
        return rect.Contains(Position)
            || GeometryMath.SegmentIntersectsRect(t[0], t[1], rect)
            || GeometryMath.SegmentIntersectsRect(t[1], t[2], rect)
            || GeometryMath.SegmentIntersectsRect(t[2], t[0], rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Position = matrix.Transform(Position);
        TextHeight *= matrix.UniformScale;
        ArrowSize *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        Point2D[] triangle = Triangle();
        surface.DrawFilledPolygon(triangle, FillStyle.Solid(stroke.Color));
        surface.DrawPolyline(triangle, closed: true, stroke);

        var textPos = new Point2D(Position.X, Position.Y + Size + TextHeight * 0.3);
        surface.DrawText(DisplayText, textPos, TextHeight, 0.0, TextHAlign.Center, TextVAlign.Bottom, null, 1.0, stroke);
    }

    public override IEntity Clone() => new ElevationDimensionEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState()
        => (Position, ZValue, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride);

    public void RestoreState(object state)
        => (Position, ZValue, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride)
            = ((Point2D, double, double, double, double, double, int, string?))state;
}
