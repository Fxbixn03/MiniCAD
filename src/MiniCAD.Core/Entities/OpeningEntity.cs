using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A recess / opening (Aussparung, #143): a negative box placed in the plan that subtracts material
/// from any wall it overlaps in 3D (via CSG). Its footprint is an axis with a width, and it has a
/// Z range (base elevation + height) — e.g. a door opening from Z=0 up 2100, or a window from
/// Z=900 up 1300. It draws in plan as its outline with diagonals (the void marker).
/// </summary>
public sealed class OpeningEntity : Entity, IEditableEntity
{
    private double _width;
    private double _height;

    public OpeningEntity(Point2D start, Point2D end, double width = 500.0, double height = 2100.0,
        double baseElevation = 0.0)
    {
        Start = start;
        End = end;
        Width = width;
        Height = height;
        BaseElevation = baseElevation;
    }

    private OpeningEntity(OpeningEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
        _width = source._width;
        _height = source._height;
        BaseElevation = source.BaseElevation;
    }

    public Point2D Start { get; set; }

    public Point2D End { get; set; }

    /// <summary>Width across the axis; always positive. Must span the wall to cut through it.</summary>
    public double Width
    {
        get => _width;
        set => _width = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z extent of the opening; always positive.</summary>
    public double Height
    {
        get => _height;
        set => _height = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z of the opening's underside (e.g. 0 = door, 900 = window sill).</summary>
    public double BaseElevation { get; set; }

    public double TopElevation => BaseElevation + _height;

    private Vector2D Normal
    {
        get
        {
            Vector2D dir = End - Start;
            if (dir.Length <= GeometryMath.Epsilon)
                return new Vector2D(0, 1);
            dir = dir.Normalized();
            return new Vector2D(-dir.Y, dir.X);
        }
    }

    /// <summary>The four footprint corners (start-left, end-left, end-right, start-right).</summary>
    public IReadOnlyList<Point2D> Footprint()
    {
        Vector2D half = Normal * (_width / 2);
        return new[] { Start + half, End + half, End - half, Start - half };
    }

    public override Rect2D Bounds
    {
        get
        {
            IReadOnlyList<Point2D> corners = Footprint();
            Rect2D bounds = Rect2D.FromPoints(corners[0], corners[0]);
            foreach (Point2D c in corners)
                bounds = bounds.Union(c);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Start;
            yield return End;
            yield return Start.Lerp(End, 0.5);
            foreach (Point2D c in Footprint())
                yield return c;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        for (int i = 0; i < corners.Count; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, corners[i], corners[(i + 1) % corners.Count], out _) <= tolerance)
                return true;
        }
        return GeometryMath.PointInPolygon(corners, point);
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        for (int i = 0; i < corners.Count; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(corners[i], corners[(i + 1) % corners.Count], rect))
                return true;
        }
        return rect.Contains(corners[0]);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
        Width *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        IReadOnlyList<Point2D> corners = Footprint();
        surface.DrawPolyline(corners, closed: true, stroke);
        // Diagonals mark it as a void/recess.
        surface.DrawLine(corners[0], corners[2], stroke);
        surface.DrawLine(corners[1], corners[3], stroke);
    }

    public override IEntity Clone() => new OpeningEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Start, GripKind.Vertex, 0),
        new Grip(End, GripKind.Vertex, 1),
        new Grip(Start.Lerp(End, 0.5), GripKind.Edge, 2),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: Start = newPosition; break;
            case 1: End = newPosition; break;
            case 2:
                Vector2D delta = newPosition - Start.Lerp(End, 0.5);
                Start += delta;
                End += delta;
                break;
        }
    }

    public object CaptureState() => (Start, End, _width, _height, BaseElevation);

    public void RestoreState(object state)
        => (Start, End, _width, _height, BaseElevation) = ((Point2D, Point2D, double, double, double))state;
}
