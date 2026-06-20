namespace MiniCAD.Core.Documents;

/// <summary>
/// The four-state activation of a Teilbild in the Allplan workflow, derived from its
/// <see cref="ElementState"/> plus whether it is the document's current (drawing-target) Teilbild:
/// <list type="bullet">
///   <item><see cref="Current"/> — active and the target for newly drawn geometry (exactly one).</item>
///   <item><see cref="Active"/> — active in the background: shown and editable, but not the target.</item>
///   <item><see cref="Passive"/> — shown dimmed, available for snapping, not editable (Locked).</item>
///   <item><see cref="Off"/> — hidden.</item>
/// </list>
/// </summary>
public enum PartialDrawingStatus
{
    Current,
    Active,
    Passive,
    Off,
}
