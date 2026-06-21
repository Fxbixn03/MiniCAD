using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A detail mark (#215): a circle around an area of interest in the plan, carrying a detail
/// identifier and an editable reference to the target detail view/sheet where the enlarged
/// drawing lives. A 2D annotation entity (persisted, picked, undoable).
/// </summary>
public sealed class DetailMarkEntity : Entity, IEditableEntity
{
    private double _radius;

    public DetailMarkEntity(Point2D center, double radius, string label = "1", string reference = "",
        double textHeight = 50.0)
    {
        Center = center;
        Radius = radius;
        Label = label ?? string.Empty;
        Reference = reference ?? string.Empty;
        TextHeight = textHeight;
    }

    private DetailMarkEntity(DetailMarkEntity source) : base(source)
    {
        Center = source.Center;
        _radius = source._radius;
        Label = source.Label;
        Reference = source.Reference;
        TextHeight = source.TextHeight;
    }

    public Point2D Center { get; set; }

    /// <summary>Circle radius in world units; always positive.</summary>
    public double Radius
    {
        get => _radius;
        set => _radius = value <= 0 ? 1.0 : value;
    }

    /// <summary>The detail identifier drawn inside/next to the circle (e.g. "1").</summary>
    public string Label { get; set; }

    /// <summary>Editable reference to the target detail view/sheet.</summary>
    public string Reference { get; set; }

    /// <summary>Label cap height in world units.</summary>
    public double TextHeight { get; set; }

    public override Rect2D Bounds
        => new(Center.X - _radius, Center.Y - _radius, Center.X + _radius, Center.Y + _radius);

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Center;
            yield return new Point2D(Center.X + _radius, Center.Y);
            yield return new Point2D(Center.X - _radius, Center.Y);
            yield return new Point2D(Center.X, Center.Y + _radius);
            yield return new Point2D(Center.X, Center.Y - _radius);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => Math.Abs((point - Center).Length - _radius) <= tolerance;

    public override bool IntersectsRect(Rect2D rect) => rect.Intersects(Bounds);

    public override void Transform(in Matrix2D matrix)
    {
        Center = matrix.Transform(Center);
        Radius *= matrix.UniformScale;
        TextHeight *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        surface.DrawCircle(Center, _radius, stroke);
        if (!string.IsNullOrEmpty(Label))
        {
            Point2D pos = new(Center.X, Center.Y + _radius + TextHeight * 0.6);
            surface.DrawText(Label, pos, TextHeight, 0, TextHAlign.Center, TextVAlign.Bottom, null, 1.0, stroke);
        }
    }

    public override IEntity Clone() => new DetailMarkEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(Center, GripKind.Vertex, 0),
        new Grip(new Point2D(Center.X + _radius, Center.Y), GripKind.Edge, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        switch (grip.Index)
        {
            case 0: Center = newPosition; break;
            case 1: Radius = (newPosition - Center).Length; break;
        }
    }

    public object CaptureState() => (Center, _radius, Label, Reference, TextHeight);

    public void RestoreState(object state)
        => (Center, _radius, Label, Reference, TextHeight) = ((Point2D, double, string, string, double))state;
}
