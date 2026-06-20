namespace MiniCAD.Core.Entities;

/// <summary>
/// Common surface of the annotation-text entities (<see cref="TextEntity"/>,
/// <see cref="MTextEntity"/>). It exposes the content plus the style-driven appearance
/// (font family, cap height, width factor) and the id of the assigned text style, so style
/// assignment and propagation can treat both uniformly.
/// </summary>
public interface ITextEntity
{
    /// <summary>Id of the assigned <c>TextStyle</c>; <see cref="Guid.Empty"/> means none.</summary>
    Guid TextStyleId { get; set; }

    string Text { get; set; }

    /// <summary>Font family name; empty means the renderer's default typeface.</summary>
    string FontFamily { get; set; }

    /// <summary>Cap height in world units.</summary>
    double Height { get; set; }

    /// <summary>Horizontal glyph scale (1.0 = normal).</summary>
    double WidthFactor { get; set; }
}
