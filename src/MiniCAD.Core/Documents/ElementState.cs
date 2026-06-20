namespace MiniCAD.Core.Documents;

/// <summary>
/// The three-state activation of a <see cref="Layer"/> or <see cref="PartialDrawing"/>
/// (Teilbild), inspired by Allplan:
/// <list type="bullet">
///   <item><see cref="Active"/> — shown and editable.</item>
///   <item><see cref="Locked"/> — shown but read-only (still available for snapping).</item>
///   <item><see cref="Off"/> — hidden and read-only.</item>
/// </list>
/// </summary>
public enum ElementState
{
    Active,
    Locked,
    Off,
}
