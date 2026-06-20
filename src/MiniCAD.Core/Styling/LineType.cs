namespace MiniCAD.Core.Styling;

/// <summary>
/// A named stroke dash pattern. The concrete dash lengths live in the renderer (device pixels,
/// so the pattern stays zoom-stable); Core only carries the symbolic choice.
/// </summary>
public enum LineType
{
    /// <summary>An unbroken line.</summary>
    Solid,

    /// <summary>Evenly spaced dashes.</summary>
    Dashed,

    /// <summary>A row of dots.</summary>
    Dotted,

    /// <summary>Dash-dot (centre-line style).</summary>
    DashDot,

    /// <summary>Dash-dot-dot.</summary>
    DashDotDot,

    /// <summary>Long-short centre line.</summary>
    Center,

    /// <summary>Short dashes for hidden edges.</summary>
    Hidden,
}
