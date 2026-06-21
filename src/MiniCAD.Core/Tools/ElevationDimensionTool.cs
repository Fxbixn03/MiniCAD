using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places level marks (Höhenkoten): each left click drops an <see cref="ElevationDimensionEntity"/>
/// at the snapped point carrying the current <see cref="ZValue"/>. The active DimStyle is applied
/// and the tool stays active for repeated placement.
/// </summary>
public sealed class ElevationDimensionTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    /// <summary>The height value written by the next placed mark.</summary>
    public double ZValue { get; set; }

    public override string Name => "Höhenkote";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        var mark = new ElevationDimensionEntity(Snap(input), ZValue);
        CadDocument.ApplyDimStyle(mark, Context.Document.ActiveDimStyle);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(mark)));
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
        {
            var preview = new ElevationDimensionEntity(_cursor, ZValue);
            CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }
}
