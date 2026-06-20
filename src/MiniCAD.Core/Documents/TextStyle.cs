namespace MiniCAD.Core.Documents;

/// <summary>
/// A named text style, analogous to a <see cref="Layer"/> but for annotation text: it carries
/// the font family, default cap height and width factor. Text entities reference a style by id
/// and adopt its values; editing the style re-applies them to every assigned text (see
/// <see cref="CadDocument.UpdateTextStyle"/>). Every document owns at least a "Standard" style.
/// </summary>
public sealed class TextStyle
{
    public TextStyle(string name, string fontFamily = "", double height = 12.0, double widthFactor = 1.0)
        : this(Guid.NewGuid(), name, fontFamily, height, widthFactor)
    {
    }

    public TextStyle(Guid id, string name, string fontFamily, double height, double widthFactor)
    {
        Id = id;
        Name = name;
        FontFamily = fontFamily ?? string.Empty;
        Height = height <= 0.0 ? 1.0 : height;
        WidthFactor = widthFactor <= 0.0 ? 1.0 : widthFactor;
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>Font family name; empty means the renderer's default typeface.</summary>
    public string FontFamily { get; set; }

    /// <summary>Default cap height in world units; always positive.</summary>
    public double Height { get; set; }

    /// <summary>Horizontal glyph scale; 1.0 is normal, &lt;1 condensed, &gt;1 expanded.</summary>
    public double WidthFactor { get; set; }

    public override string ToString() => Name;
}
