using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>How a <see cref="LinearDimensionEntity"/> orients its dimension line.</summary>
public enum LinearDimensionKind
{
    /// <summary>Parallel to the line between the two points (true distance).</summary>
    Aligned,

    /// <summary>Horizontal dimension line (measures the X distance).</summary>
    Horizontal,

    /// <summary>Vertical dimension line (measures the Y distance).</summary>
    Vertical,
}

/// <summary>
/// A linear dimension between two points. The dimension line passes through
/// <see cref="DimLinePoint"/> (which sets the offset and side) in a direction chosen by
/// <see cref="Kind"/>; the measured value is the distance between the two points projected onto
/// that line. Renders extension lines, the dimension line, arrowheads and the measurement text.
/// </summary>
public sealed class LinearDimensionEntity : DimensionEntity, IEditableEntity
{
    public LinearDimensionEntity(Point2D p1, Point2D p2, Point2D dimLinePoint,
        LinearDimensionKind kind = LinearDimensionKind.Aligned)
    {
        P1 = p1;
        P2 = p2;
        DimLinePoint = dimLinePoint;
        Kind = kind;
    }

    private LinearDimensionEntity(LinearDimensionEntity source) : base(source)
    {
        P1 = source.P1;
        P2 = source.P2;
        DimLinePoint = source.DimLinePoint;
        Kind = source.Kind;
    }

    /// <summary>First measured point.</summary>
    public Point2D P1 { get; set; }

    /// <summary>Second measured point.</summary>
    public Point2D P2 { get; set; }

    /// <summary>A point the dimension line runs through (defines the offset distance and side).</summary>
    public Point2D DimLinePoint { get; set; }

    public LinearDimensionKind Kind { get; set; }

    private Vector2D Direction => Kind switch
    {
        LinearDimensionKind.Horizontal => Vector2D.UnitX,
        LinearDimensionKind.Vertical => Vector2D.UnitY,
        _ => (P2 - P1).Normalized() is { } d && d != Vector2D.Zero ? d : Vector2D.UnitX,
    };

    /// <summary>Projects a point onto the dimension line.</summary>
    private Point2D Project(Point2D p, Vector2D dir)
        => DimLinePoint + dir * (p - DimLinePoint).Dot(dir);

    private (Point2D M1, Point2D M2, Vector2D Dir, Vector2D Normal) Geometry()
    {
        Vector2D dir = Direction;
        Point2D m1 = Project(P1, dir);
        Point2D m2 = Project(P2, dir);

        // Normal pointing from the measured points toward the dimension line (the offset side).
        Vector2D normal = (m1 - P1);
        if (normal == Vector2D.Zero)
            normal = (m2 - P2);
        normal = normal == Vector2D.Zero ? dir.Perpendicular() : normal.Normalized();

        return (m1, m2, dir, normal);
    }

    public override double MeasuredValue
    {
        get
        {
            (Point2D m1, Point2D m2, _, _) = Geometry();
            return m1.DistanceTo(m2);
        }
    }

    public override Rect2D Bounds
    {
        get
        {
            (Point2D m1, Point2D m2, _, _) = Geometry();
            Rect2D bounds = Rect2D.FromPoints(P1, P2).Union(m1).Union(m2);
            return bounds.Inflate(TextHeight);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return P1;
            yield return P2;
            (Point2D m1, Point2D m2, _, _) = Geometry();
            yield return m1;
            yield return m2;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        (Point2D m1, Point2D m2, _, _) = Geometry();
        return GeometryMath.DistancePointToSegment(point, m1, m2, out _) <= tolerance
            || GeometryMath.DistancePointToSegment(point, P1, m1, out _) <= tolerance
            || GeometryMath.DistancePointToSegment(point, P2, m2, out _) <= tolerance;
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        (Point2D m1, Point2D m2, _, _) = Geometry();
        return GeometryMath.SegmentIntersectsRect(m1, m2, rect)
            || GeometryMath.SegmentIntersectsRect(P1, m1, rect)
            || GeometryMath.SegmentIntersectsRect(P2, m2, rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        P1 = matrix.Transform(P1);
        P2 = matrix.Transform(P2);
        DimLinePoint = matrix.Transform(DimLinePoint);
        double scale = matrix.UniformScale;
        TextHeight *= scale;
        ArrowSize *= scale;
        ExtensionOffset *= scale;
        ExtensionOvershoot *= scale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        (Point2D m1, Point2D m2, Vector2D dir, Vector2D normal) = Geometry();

        DrawExtensionLine(surface, P1, m1, stroke);
        DrawExtensionLine(surface, P2, m2, stroke);

        surface.DrawLine(m1, m2, stroke);
        DrawArrow(surface, m1, m1 - m2, stroke);
        DrawArrow(surface, m2, m2 - m1, stroke);

        Point2D mid = m1.Lerp(m2, 0.5) + normal * (TextHeight * 0.5);
        DrawMeasurementText(surface, mid, Math.Atan2(dir.Y, dir.X), stroke);
    }

    private void DrawExtensionLine(IRenderSurface surface, Point2D from, Point2D to, in StrokeStyle stroke)
    {
        Vector2D e = to - from;
        if (e.Length <= GeometryMath.Epsilon)
            return;

        Vector2D dir = e.Normalized();
        Point2D start = from + dir * ExtensionOffset;
        Point2D end = to + dir * ExtensionOvershoot;
        surface.DrawLine(start, end, stroke);
    }

    public override IEntity Clone() => new LinearDimensionEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(P1, GripKind.Vertex, 0),
        new Grip(P2, GripKind.Vertex, 1),
        new Grip(DimLinePoint, GripKind.Edge, 2),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: P1 = newPosition; break;
            case 1: P2 = newPosition; break;
            case 2: DimLinePoint = newPosition; break;
        }
    }

    public object CaptureState()
        => (P1, P2, DimLinePoint, Kind, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride);

    public void RestoreState(object state)
        => (P1, P2, DimLinePoint, Kind, TextHeight, ArrowSize, ExtensionOffset, ExtensionOvershoot, DecimalPlaces, TextOverride)
            = ((Point2D, Point2D, Point2D, LinearDimensionKind, double, double, double, double, int, string?))state;
}
