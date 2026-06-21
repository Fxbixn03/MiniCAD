using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Places an architectural column (#75) at the clicked point; size/height come from settings.</summary>
public sealed class ColumnTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    public bool Round { get; set; }
    public double Width { get; set; } = 300.0;
    public double Depth { get; set; } = 300.0;
    public double Height { get; set; } = 2500.0;
    public double BaseElevation { get; set; }

    public override string Name => "Stütze";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }
        if (input.Button != ToolButton.Left)
            return;

        Point2D p = Snap(input);
        Context.Execute(new AddEntityCommand(Context.Document,
            ApplyDefaultStyle(new ColumnEntity(p, Round, Width, Depth, Height, BaseElevation))));
        ClearSnap();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _hasCursor = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasCursor)
            items.Add(new OverlayItem(new ColumnEntity(_cursor, Round, Width, Depth, Height, BaseElevation), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
