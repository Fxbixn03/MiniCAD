using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places reference points (nodes): each left click drops a <see cref="PointEntity"/> at the
/// snapped cursor. Marker size and style come from the inline options bar. Stays active for
/// repeated placement.
/// </summary>
public sealed class PointTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    public double Size { get; set; } = 6.0;

    public PointStyle Style { get; set; } = PointStyle.Plus;

    public override string Name => "Punkt";

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
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(new PointEntity(world, Size, Style))));
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasCursor)
            items.Add(new OverlayItem(new PointEntity(_cursor, Size, Style), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
