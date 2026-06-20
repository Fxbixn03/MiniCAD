using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// A one-shot tool that moves the document's origin (Nullpunkt). The next left click sets the
/// origin to the snapped cursor position and hands control back to the select tool; a right
/// click or Escape cancels.
/// </summary>
public sealed class SetNullPointTool : ToolBase
{
    public override string Name => "Nullpunkt setzen";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        Point2D world = Snap(input);
        Context.Document.CoordinateSystem.SetOrigin(world);
        ClearSnap();
        Context.RequestRedraw();
        Context.ReturnToDefaultTool();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        Snap(input);
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(1);
        AddSnapMarker(items);
        return items;
    }
}
