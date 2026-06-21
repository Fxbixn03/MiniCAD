namespace MiniCAD.Core.Documents;

/// <summary>
/// A named dimension style, analogous to <see cref="TextStyle"/> but for dimensions: it bundles
/// the text height, arrowhead size, extension-line offset/overshoot and rounding. Dimension
/// entities reference a style by id and adopt its values; editing a style re-applies them to
/// every assigned dimension. Every document owns at least a "Standard" dimension style.
/// </summary>
public sealed class DimStyle
{
    public DimStyle(string name, double textHeight = 12.0, double arrowSize = 10.0,
        double extensionOffset = 3.0, double extensionOvershoot = 5.0, int decimalPlaces = 0)
        : this(Guid.NewGuid(), name, textHeight, arrowSize, extensionOffset, extensionOvershoot, decimalPlaces)
    {
    }

    public DimStyle(Guid id, string name, double textHeight, double arrowSize,
        double extensionOffset, double extensionOvershoot, int decimalPlaces)
    {
        Id = id;
        Name = name;
        TextHeight = textHeight <= 0.0 ? 1.0 : textHeight;
        ArrowSize = arrowSize < 0.0 ? 0.0 : arrowSize;
        ExtensionOffset = extensionOffset < 0.0 ? 0.0 : extensionOffset;
        ExtensionOvershoot = extensionOvershoot < 0.0 ? 0.0 : extensionOvershoot;
        DecimalPlaces = decimalPlaces < 0 ? 0 : decimalPlaces;
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>Height of the measurement text in world units.</summary>
    public double TextHeight { get; set; }

    /// <summary>Arrowhead length in world units (0 = no arrows).</summary>
    public double ArrowSize { get; set; }

    /// <summary>Gap between the measured point and the start of its extension line.</summary>
    public double ExtensionOffset { get; set; }

    /// <summary>How far the extension lines run past the dimension line.</summary>
    public double ExtensionOvershoot { get; set; }

    /// <summary>Decimal places the measurement value is rounded to.</summary>
    public int DecimalPlaces { get; set; }

    public override string ToString() => Name;
}
