using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A section mark (#215): a cutting line drawn in the plan with view arrows at both ends, a
/// section identifier (e.g. "A") and an editable reference to the target view/sheet where the
/// associated section drawing lives. It is a real 2D annotation entity (persisted, picked,
/// undoable) and is the plan-side companion of the associative section (<see cref="Sections.SectionGenerator"/>).
/// </summary>
public sealed class SectionMarkEntity : Entity, IEditableEntity
{
    public SectionMarkEntity(Point2D start, Point2D end, string label = "A", string reference = "",
        double size = 50.0, bool flipDirection = false)
    {
        Start = start;
        End = end;
        Label = label ?? string.Empty;
        Reference = reference ?? string.Empty;
        Size = size;
        FlipDirection = flipDirection;
    }

    private SectionMarkEntity(SectionMarkEntity source) : base(source)
    {
        Start = source.Start;
        End = source.End;
        Label = source.Label;
        Reference = source.Reference;
        Size = source.Size;
        FlipDirection = source.FlipDirection;
    }

    public Point2D Start { get; set; }

    public Point2D End { get; set; }

    /// <summary>The section identifier, drawn at both ends (e.g. "A" → the section "A–A").</summary>
    public string Label { get; set; }

    /// <summary>Editable reference to the target view/sheet (e.g. "Blatt 2" or detail number).</summary>
    public string Reference { get; set; }

    /// <summary>Symbol size in world units (arrow length and label cap height).</summary>
    public double Size { get; set; }

    /// <summary>Which side the view arrows (looking direction) point towards.</summary>
    public bool FlipDirection { get; set; }

    /// <summary>Unit direction along the cut line.</summary>
    private Vector2D Direction
    {
        get
        {
            Vector2D d = End - Start;
            return d.Length <= GeometryMath.Epsilon ? new Vector2D(1, 0) : d.Normalized();
        }
    }

    /// <summary>Unit view direction (perpendicular to the line), the way the section looks.</summary>
    private Vector2D ViewDirection
    {
        get
        {
            Vector2D d = Direction;
            var n = new Vector2D(-d.Y, d.X);
            return FlipDirection ? -n : n;
        }
    }

    public override Rect2D Bounds
    {
        get
        {
            Rect2D box = Rect2D.FromPoints(Start, End);
            return box.Inflate(Size);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Start;
            yield return End;
            yield return Start.Lerp(End, 0.5);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => GeometryMath.DistancePointToSegment(point, Start, End, out _) <= tolerance;

    public override bool IntersectsRect(Rect2D rect)
        => GeometryMath.SegmentIntersectsRect(Start, End, rect);

    public override void Transform(in Matrix2D matrix)
    {
        Start = matrix.Transform(Start);
        End = matrix.Transform(End);
        Size *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        surface.DrawLine(Start, End, stroke);
        DrawArrow(surface, Start, stroke);
        DrawArrow(surface, End, stroke);

        if (!string.IsNullOrEmpty(Label))
        {
            DrawLabel(surface, Start, stroke);
            DrawLabel(surface, End, stroke);
        }
    }

    private void DrawArrow(IRenderSurface surface, Point2D at, in StrokeStyle stroke)
    {
        Vector2D view = ViewDirection;
        Point2D tip = at + view * Size;
        surface.DrawLine(at, tip, stroke);

        // small arrowhead
        Vector2D back = -view;
        Vector2D side = Direction;
        double h = Size * 0.3;
        surface.DrawLine(tip, tip + back * h + side * (h * 0.5), stroke);
        surface.DrawLine(tip, tip + back * h - side * (h * 0.5), stroke);
    }

    private void DrawLabel(IRenderSurface surface, Point2D at, in StrokeStyle stroke)
    {
        Point2D pos = at + ViewDirection * (Size * 1.25);
        surface.DrawText(Label, pos, Size, 0, TextHAlign.Center, TextVAlign.Middle, null, 1.0, stroke);
    }

    public override IEntity Clone() => new SectionMarkEntity(this);

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

    public object CaptureState() => (Start, End, Label, Reference, Size, FlipDirection);

    public void RestoreState(object state)
        => (Start, End, Label, Reference, Size, FlipDirection) = ((Point2D, Point2D, string, string, double, bool))state;
}
