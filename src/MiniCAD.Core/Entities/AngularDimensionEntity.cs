using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An angular dimension between two rays sharing a vertex. <see cref="Vertex"/> is the corner,
/// <see cref="P1"/>/<see cref="P2"/> give the two ray directions, and <see cref="ArcPoint"/>
/// sets the arc radius and which of the two angles is measured. Renders the radial extension
/// lines, an arc dimension line with arrows and the angle in degrees.
/// </summary>
public sealed class AngularDimensionEntity : DimensionEntity, IEditableEntity
{
    private const int ArcSegments = 24;

    public AngularDimensionEntity(Point2D vertex, Point2D p1, Point2D p2, Point2D arcPoint)
    {
        Vertex = vertex;
        P1 = p1;
        P2 = p2;
        ArcPoint = arcPoint;
    }

    private AngularDimensionEntity(AngularDimensionEntity source) : base(source)
    {
        Vertex = source.Vertex;
        P1 = source.P1;
        P2 = source.P2;
        ArcPoint = source.ArcPoint;
    }

    public Point2D Vertex { get; set; }
    public Point2D P1 { get; set; }
    public Point2D P2 { get; set; }

    /// <summary>A point on the arc dimension line (sets its radius and the measured side).</summary>
    public Point2D ArcPoint { get; set; }

    protected override string Suffix => "°";

    private (double Start, double Sweep, double Radius) Arc()
    {
        double aA = Math.Atan2(P1.Y - Vertex.Y, P1.X - Vertex.X);
        double aB = Math.Atan2(P2.Y - Vertex.Y, P2.X - Vertex.X);
        double aR = Math.Atan2(ArcPoint.Y - Vertex.Y, ArcPoint.X - Vertex.X);

        double ccw = Norm2Pi(aB - aA);
        double rRel = Norm2Pi(aR - aA);
        double sweep = rRel <= ccw ? ccw : ccw - 2 * Math.PI; // pick the arc that contains ArcPoint

        double radius = Vertex.DistanceTo(ArcPoint);
        return (aA, sweep, radius);
    }

    private static double Norm2Pi(double a)
    {
        a %= 2 * Math.PI;
        return a < 0 ? a + 2 * Math.PI : a;
    }

    private static Point2D OnArc(Point2D center, double radius, double angle)
        => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));

    public override double MeasuredValue
    {
        get
        {
            (_, double sweep, _) = Arc();
            return Math.Abs(GeometryMath.RadiansToDegrees(sweep));
        }
    }

    public override Rect2D Bounds
    {
        get
        {
            (double start, double sweep, double radius) = Arc();
            Rect2D bounds = Rect2D.FromPoints(Vertex, ArcPoint).Union(P1).Union(P2);
            for (int i = 0; i <= ArcSegments; i++)
                bounds = bounds.Union(OnArc(Vertex, radius, start + sweep * i / ArcSegments));
            return bounds.Inflate(TextHeight);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Vertex;
            yield return P1;
            yield return P2;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        (double start, double sweep, double radius) = Arc();
        double d = Vertex.DistanceTo(point);
        if (Math.Abs(d - radius) <= tolerance)
        {
            double angle = Math.Atan2(point.Y - Vertex.Y, point.X - Vertex.X);
            if (GeometryMath.AngleInSweep(start, sweep, angle))
                return true;
        }

        return false;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        (double start, double sweep, double radius) = Arc();
        Point2D prev = OnArc(Vertex, radius, start);
        for (int i = 1; i <= ArcSegments; i++)
        {
            Point2D next = OnArc(Vertex, radius, start + sweep * i / ArcSegments);
            if (GeometryMath.SegmentIntersectsRect(prev, next, rect))
                return true;
            prev = next;
        }

        return false;
    }

    public override void Transform(in Matrix2D matrix)
    {
        Vertex = matrix.Transform(Vertex);
        P1 = matrix.Transform(P1);
        P2 = matrix.Transform(P2);
        ArcPoint = matrix.Transform(ArcPoint);
        double scale = matrix.UniformScale;
        TextHeight *= scale;
        ArrowSize *= scale;
        ExtensionOffset *= scale;
        ExtensionOvershoot *= scale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        (double start, double sweep, double radius) = Arc();
        if (radius <= GeometryMath.Epsilon)
            return;

        double end = start + sweep;
        Vector2D dirA = new Vector2D(Math.Cos(start), Math.Sin(start));
        Vector2D dirB = new Vector2D(Math.Cos(end), Math.Sin(end));

        // Radial extension lines from near the vertex out past the arc.
        surface.DrawLine(Vertex + dirA * ExtensionOffset, Vertex + dirA * (radius + ExtensionOvershoot), stroke);
        surface.DrawLine(Vertex + dirB * ExtensionOffset, Vertex + dirB * (radius + ExtensionOvershoot), stroke);

        surface.DrawArc(Vertex, radius, start, sweep, stroke);

        Point2D pStart = OnArc(Vertex, radius, start);
        Point2D pEnd = OnArc(Vertex, radius, end);
        double sign = Math.Sign(sweep);
        // Tangents along the arc (into the sweep) for the arrowheads.
        DrawArrow(surface, pStart, new Vector2D(-Math.Sin(start), Math.Cos(start)) * sign, stroke);
        DrawArrow(surface, pEnd, new Vector2D(-Math.Sin(end), Math.Cos(end)) * -sign, stroke);

        double mid = start + sweep / 2;
        Point2D textPos = OnArc(Vertex, radius + TextHeight * 0.5, mid);
        DrawMeasurementText(surface, textPos, mid + Math.PI / 2, stroke);
    }

    public override IEntity Clone() => new AngularDimensionEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Vertex, GripKind.Vertex, 0),
        new Grip(P1, GripKind.Vertex, 1),
        new Grip(P2, GripKind.Vertex, 2),
        new Grip(ArcPoint, GripKind.Edge, 3),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: Vertex = newPosition; break;
            case 1: P1 = newPosition; break;
            case 2: P2 = newPosition; break;
            case 3: ArcPoint = newPosition; break;
        }
    }

    public object CaptureState()
        => (Vertex, P1, P2, ArcPoint, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride);

    public void RestoreState(object state)
        => (Vertex, P1, P2, ArcPoint, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride)
            = ((Point2D, Point2D, Point2D, Point2D, double, double, double, double, int, string?))state;
}
