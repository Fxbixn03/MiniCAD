using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An architectural beam / downstand (Unterzug/Träger, #144): a horizontal member along an axis with
/// a cross-section width and height, usually elevated (its base at ceiling level). It draws in plan
/// as its footprint with a diagonal marker (it sits above the cut plane) and is the 2D source of a
/// live 3D box solid.
/// </summary>
public sealed class BeamEntity : Entity, IEditableEntity
{
    private double _width;
    private double _height;

    public BeamEntity(Point2D start, Point2D end, double width = 240.0, double height = 400.0,
        double baseElevation = 2500.0)
    {
        Start = start;
        End = end;
        Width = width;
        Height = height;
        BaseElevation = baseElevation;
    }

    private BeamEntity(BeamEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
        _width = source._width;
        _height = source._height;
        BaseElevation = source.BaseElevation;
    }

    public Point2D Start { get; set; }

    public Point2D End { get; set; }

    /// <summary>Cross-section width across the axis; always positive.</summary>
    public double Width
    {
        get => _width;
        set => _width = value <= 0 ? 1.0 : value;
    }

    /// <summary>Cross-section height (Z extent); always positive.</summary>
    public double Height
    {
        get => _height;
        set => _height = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z of the beam's underside (often the ceiling level).</summary>
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
            IReadOnlyList<Point2D> c = Footprint();
            Rect2D bounds = Rect2D.FromPoints(c[0], c[0]);
            foreach (Point2D p in c)
                bounds = bounds.Union(p);
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
        IReadOnlyList<Point2D> c = Footprint();
        for (int i = 0; i < c.Count; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, c[i], c[(i + 1) % c.Count], out _) <= tolerance)
                return true;
        }
        return GeometryMath.PointInPolygon(c, point);
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        IReadOnlyList<Point2D> c = Footprint();
        for (int i = 0; i < c.Count; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(c[i], c[(i + 1) % c.Count], rect))
                return true;
        }
        return rect.Contains(c[0]);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
        Width *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        IReadOnlyList<Point2D> c = Footprint();
        surface.DrawPolyline(c, closed: true, stroke);
        // Diagonal markers — a beam sits above the cut plane (drawn like a hidden/overhead member).
        surface.DrawLine(c[0], c[2], stroke);
        surface.DrawLine(c[1], c[3], stroke);
    }

    public override IEntity Clone() => new BeamEntity(this);

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
