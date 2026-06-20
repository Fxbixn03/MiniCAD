namespace MiniCAD.App.Input;

/// <summary>
/// Every command/tool that can be bound to a keyboard shortcut. The enum is the stable key
/// for persistence, so values must not be renumbered (names are stored, not ordinals).
/// </summary>
public enum ShortcutAction
{
    Select,
    Line,
    Rectangle,
    Circle,
    Polyline,
    Delete,
    Undo,
    Redo,
    ZoomToFit,
    ToggleSnap,
    SetNullPoint,
    NewProject,
    OpenProject,
    Save,
    SaveAs,
    Move,
    Copy,
    Rotate,
    Mirror,
    Scale,
    Offset,
    Trim,
    Stretch,
    Fillet,
    Array,
}
