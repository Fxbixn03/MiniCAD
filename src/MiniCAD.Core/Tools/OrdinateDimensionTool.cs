using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places a coordinate (ordinate) dimension: click the point to label, then the leader/text
/// location. The coordinates are taken relative to the document's Nullpunkt and the active
/// DimStyle is applied.
/// </summary>
public sealed class OrdinateDimensionTool : ToolBase
{
    private Point2D? _position;
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Koordinatenmaß";

    protected override bool HasActiveOperation => _position is not null;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            if (HasActiveOperation)
                Cancel();
            else
                Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        Point2D world = Snap(input);
        if (_position is null)
        {
            _position = world;
        }
        else
        {
            Point2D origin = Context.Document.CoordinateSystem.Origin.ToPoint2D();
            var dim = new OrdinateDimensionEntity(_position.Value, world, origin);
            CadDocument.ApplyDimStyle(dim, Context.Document.ActiveDimStyle);
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(dim)));
            Cancel();
        }

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
        _position = null;
        ClearSnap();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_position is { } p && _hasCursor)
        {
            Point2D origin = Context.Document.CoordinateSystem.Origin.ToPoint2D();
            var preview = new OrdinateDimensionEntity(p, _cursor, origin);
            CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }
}
