namespace MiniCAD.Core.Snapping;

/// <summary>User-controllable snapping options.</summary>
public sealed class SnapSettings
{
    /// <summary>Master switch for all snapping.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Snap to characteristic points of existing entities (endpoints, centers, …).</summary>
    public bool SnapToPoints { get; set; } = true;

    /// <summary>Snap to the reference grid when no object snap is in range.</summary>
    public bool SnapToGrid { get; set; } = true;
}
