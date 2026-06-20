using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tools;

public enum ToolButton
{
    None,
    Left,
    Middle,
    Right,
}

[Flags]
public enum ToolModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
}

/// <summary>Keys that tools react to, kept abstract so Core never references a UI framework.</summary>
public enum ToolKey
{
    None,
    Escape,
    Enter,
    Delete,
}

/// <summary>
/// A framework-agnostic pointer event delivered to a tool. The host (App) is responsible
/// for translating its native input into world coordinates before handing it over.
/// </summary>
public readonly struct ToolPointerInput
{
    public required Point2D World { get; init; }

    public ToolButton Button { get; init; }

    public ToolModifiers Modifiers { get; init; }

    /// <summary>Consecutive click count (2 = double-click), used e.g. to finish a polyline.</summary>
    public int ClickCount { get; init; }

    public bool HasShift => (Modifiers & ToolModifiers.Shift) != 0;

    public bool HasControl => (Modifiers & ToolModifiers.Control) != 0;
}
