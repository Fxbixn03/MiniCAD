namespace MiniCAD.Core.Rendering;

/// <summary>Horizontal anchoring of a text run relative to its insertion point.</summary>
public enum TextHAlign
{
    /// <summary>Insertion point at the left edge; text extends to the right.</summary>
    Left,

    /// <summary>Insertion point at the horizontal centre of the text.</summary>
    Center,

    /// <summary>Insertion point at the right edge; text extends to the left.</summary>
    Right,
}

/// <summary>Vertical anchoring of a text run relative to its insertion point.</summary>
public enum TextVAlign
{
    /// <summary>Insertion point at the top of the cap box; text hangs below it.</summary>
    Top,

    /// <summary>Insertion point at the vertical middle of the text.</summary>
    Middle,

    /// <summary>Insertion point on the typographic baseline.</summary>
    Baseline,

    /// <summary>Insertion point at the bottom (descender line); text sits above it.</summary>
    Bottom,
}
