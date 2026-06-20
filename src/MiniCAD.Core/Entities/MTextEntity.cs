using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A multi-line paragraph of annotation text. Lines break on explicit newlines and, when a
/// positive <see cref="Width"/> is set, are additionally word-wrapped to fit that width
/// (estimated from the cap height, so Core needs no font metrics). Each resolved line is drawn
/// top-down through <see cref="IRenderSurface.DrawText"/>.
/// </summary>
public sealed class MTextEntity : Entity, IEditableEntity, ITextEntity
{
    /// <summary>Line advance as a multiple of the cap height.</summary>
    internal const double LineSpacing = 1.35;

    private double _height;
    private double _width;
    private double _widthFactor = 1.0;

    public MTextEntity(Point2D position, string text, double height = 12.0, double width = 0.0,
        double rotation = 0.0, TextHAlign horizontalAlignment = TextHAlign.Left,
        TextVAlign verticalAlignment = TextVAlign.Top)
    {
        Position = position;
        Text = text ?? string.Empty;
        Height = height;
        Width = width;
        Rotation = rotation;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
    }

    private MTextEntity(MTextEntity source) : base(source)
    {
        Position = source.Position;
        Text = source.Text;
        _height = source._height;
        _width = source._width;
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

    /// <summary>Wrap width in world units; <c>0</c> (or less) wraps only on explicit newlines.</summary>
    public double Width
    {
        get => _width;
        set => _width = Math.Max(0.0, value);
    }

    /// <summary>Paragraph rotation in radians, CCW from the world X axis.</summary>
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

    private double LineHeight => _height * LineSpacing;

    /// <summary>Transform from the unrotated text-local frame (origin = insertion point) to world.</summary>
    private Matrix2D ToWorld => Matrix2D.Rotation(Rotation) * Matrix2D.Translation(Position.X, Position.Y);

    /// <summary>Splits the content into display lines, word-wrapping to <see cref="Width"/> when set.</summary>
    public IReadOnlyList<string> WrappedLines()
    {
        var lines = new List<string>();
        foreach (string paragraph in Text.Replace("\r\n", "\n").Split('\n'))
            WrapParagraph(paragraph, lines);

        if (lines.Count == 0)
            lines.Add(string.Empty);
        return lines;
    }

    private void WrapParagraph(string paragraph, List<string> lines)
    {
        if (_width <= 0.0)
        {
            lines.Add(paragraph);
            return;
        }

        string current = string.Empty;
        foreach (string word in paragraph.Split(' '))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && EstimateWidth(candidate) > _width)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);
    }

    private double EstimateWidth(string text) => text.Length * _height * TextEntity.GlyphAspect * _widthFactor;

    private double BoxWidth
    {
        get
        {
            if (_width > 0.0)
                return _width;
            return WrappedLines().Select(EstimateWidth).DefaultIfEmpty(0.0).Max();
        }
    }

    private Rect2D LocalBox
        => TextBox.Local(BoxWidth, WrappedLines().Count * LineHeight, HorizontalAlignment, VerticalAlignment);

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
        Width *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        IReadOnlyList<string> lines = WrappedLines();
        Matrix2D toWorld = ToWorld;
        double lineHeight = LineHeight;
        double topY = LocalBox.MaxY; // local top edge; lines flow downward from here

        for (int i = 0; i < lines.Count; i++)
        {
            Point2D insertion = toWorld.Transform(new Point2D(0.0, topY - i * lineHeight));
            surface.DrawText(lines[i], insertion, Height, Rotation, HorizontalAlignment, TextVAlign.Top,
                FontFamily, WidthFactor, stroke);
        }
    }

    public override IEntity Clone() => new MTextEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState()
        => (Position, Text, Height, Width, Rotation, HorizontalAlignment, VerticalAlignment,
            TextStyleId, FontFamily, WidthFactor);

    public void RestoreState(object state)
        => (Position, Text, Height, Width, Rotation, HorizontalAlignment, VerticalAlignment,
            TextStyleId, FontFamily, WidthFactor)
            = ((Point2D, string, double, double, double, TextHAlign, TextVAlign, Guid, string, double))state;
}
