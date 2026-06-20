using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>A straight line segment between two points.</summary>
public sealed class LineEntity : Entity, IEditableEntity
{
    public LineEntity(Point2D start, Point2D end)
    {
        Start = start;
        End = end;
    }

    private LineEntity(LineEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
    }

    public Point2D Start { get; set; }

    public Point2D End { get; set; }

    public override Rect2D Bounds => Rect2D.FromPoints(Start, End);

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Start;
            yield return End;
            yield return Start.Lerp(End, 0.5); // midpoint
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => GeometryMath.DistancePointToSegment(point, Start, End, out _) <= tolerance;

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawLine(Start, End, stroke);

    public override IEntity Clone() => new LineEntity(this);

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
            case 0:
                Start = newPosition;
                break;
            case 1:
                End = newPosition;
                break;
            case 2: // edge midpoint: slide the whole segment
                Vector2D delta = newPosition - Start.Lerp(End, 0.5);
                Start += delta;
                End += delta;
                break;
        }
    }

    public object CaptureState() => (Start, End);

    public void RestoreState(object state)
    {
        (Start, End) = ((Point2D, Point2D))state;
    }
}
