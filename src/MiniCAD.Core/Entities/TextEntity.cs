using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A single line of annotation text anchored at an insertion point. It carries its content,
/// cap height (world units), baseline rotation and horizontal/vertical alignment, and draws
/// itself through the backend-agnostic <see cref="IRenderSurface.DrawText"/> primitive. The
/// bounding box used for picking and snapping is approximated from the cap height and an
/// average glyph aspect ratio so Core stays free of any font/measurement dependency.
/// </summary>
public sealed class TextEntity : Entity, IEditableEntity, ITextEntity
{
    /// <summary>Average glyph advance as a fraction of cap height; used to estimate text width.</summary>
    internal const double GlyphAspect = 0.6;

    private double _height;
    private double _widthFactor = 1.0;

    public TextEntity(Point2D position, string text, double height = 12.0, double rotation = 0.0,
        TextHAlign horizontalAlignment = TextHAlign.Left, TextVAlign verticalAlignment = TextVAlign.Baseline)
    {
        Position = position;
        Text = text ?? string.Empty;
        Height = height;
        Rotation = rotation;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
    }

    private TextEntity(TextEntity source) : base(source)
    {
        Position = source.Position;
        Text = source.Text;
        _height = source._height;
        Rotation = source.Rotation;
        HorizontalAlignment = source.HorizontalAlignment;
        VerticalAlignment = source.VerticalAlignment;
        TextStyleId = source.TextStyleId;
        FontFamily = source.FontFamily;
        _widthFactor = source._widthFactor;
    }

    /// <summary>The insertion (anchor) point in world coordinates.</summary>
    public Point2D Position { get; set; }

    public string Text { get; set; }

    /// <summary>Cap height in world units; always positive.</summary>
    public double Height
    {
        get => _height;
        set => _height = value <= 0.0 ? 1.0 : value;
    }

    /// <summary>Baseline angle in radians, CCW from the world X axis.</summary>
    public double Rotation { get; set; }

    public TextHAlign HorizontalAlignment { get; set; }

    public TextVAlign VerticalAlignment { get; set; }

    /// <summary>Id of the assigned text style (<see cref="Guid.Empty"/> = none); see <c>TextStyle</c>.</summary>
    public Guid TextStyleId { get; set; }

    /// <summary>Font family; empty means the renderer default. Driven by the assigned text style.</summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <summary>Horizontal glyph scale (1.0 = normal); always positive.</summary>
    public double WidthFactor
    {
        get => _widthFactor;
        set => _widthFactor = value <= 0.0 ? 1.0 : value;
    }

    /// <summary>Estimated text width in world units (cap height × glyph aspect × width factor × chars).</summary>
    private double EstimatedWidth => Text.Length * _height * GlyphAspect * _widthFactor;

    /// <summary>Transform from the unrotated text-local frame (origin = insertion point) to world.</summary>
    private Matrix2D ToWorld => Matrix2D.Rotation(Rotation) * Matrix2D.Translation(Position.X, Position.Y);

    /// <summary>The text box in the unrotated local frame, anchored per the alignment.</summary>
    private Rect2D LocalBox => TextBox.Local(EstimatedWidth, _height, HorizontalAlignment, VerticalAlignment);

    /// <summary>The four world-space corners of the (rotated) text box.</summary>
    private Point2D[] Corners() => TextBox.Corners(LocalBox, ToWorld);

    public override Rect2D Bounds => TextBox.Bounds(Corners());

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            foreach (Point2D corner in Corners())
                yield return corner;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => TextBox.Contains(LocalBox, ToWorld, point, tolerance);

    public override bool IntersectsRect(Rect2D rect) => TextBox.IntersectsRect(Corners(), rect);

    public override void Transform(in Matrix2D matrix)
    {
        var axis = new Vector2D(Math.Cos(Rotation), Math.Sin(Rotation));
        Vector2D transformedAxis = matrix.TransformVector(axis);

        Position = matrix.Transform(Position);
        Height *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawText(Text, Position, Height, Rotation, HorizontalAlignment, VerticalAlignment,
            FontFamily, WidthFactor, stroke);

    public override IEntity Clone() => new TextEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState()
        => (Position, Text, Height, Rotation, HorizontalAlignment, VerticalAlignment, TextStyleId, FontFamily, WidthFactor);

    public void RestoreState(object state)
        => (Position, Text, Height, Rotation, HorizontalAlignment, VerticalAlignment, TextStyleId, FontFamily, WidthFactor)
            = ((Point2D, string, double, double, TextHAlign, TextVAlign, Guid, string, double))state;
}
