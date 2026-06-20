using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// An interactive editing tool (select, draw line, draw circle, …). Tools are the main
/// extension point for editor interactions: a new tool implements this interface and is
/// driven by the <see cref="ToolManager"/> via abstract pointer/key input.
/// </summary>
public interface ITool
{
    string Name { get; }

    /// <summary>Called when the tool becomes active; receives the context it operates against.</summary>
    void Activate(IToolContext context);

    /// <summary>Called when another tool takes over; should abort any in-progress operation.</summary>
    void Deactivate();

    void PointerDown(in ToolPointerInput input);

    void PointerMove(in ToolPointerInput input);

    void PointerUp(in ToolPointerInput input);

    void KeyDown(ToolKey key);

    /// <summary>Aborts the current in-progress operation, leaving the document untouched.</summary>
    void Cancel();

    /// <summary>Transient geometry to draw on top of the document (rubber-band preview).</summary>
    IReadOnlyList<OverlayItem> GetOverlay();
}
