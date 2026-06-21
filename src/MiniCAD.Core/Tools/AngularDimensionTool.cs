using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places an angular dimension by four clicks: the vertex, the two points defining the rays,
/// then a point on the arc (its radius and the measured side). The active <see cref="DimStyle"/>
/// is applied and the angle computed automatically.
/// </summary>
public sealed class AngularDimensionTool : ToolBase
{
    private Point2D? _vertex;
    private Point2D? _p1;
    private Point2D? _p2;
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Winkelmaß";

    protected override bool HasActiveOperation => _vertex is not null;

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
        if (_vertex is null)
            _vertex = world;
        else if (_p1 is null)
            _p1 = world;
        else if (_p2 is null)
            _p2 = world;
        else
        {
            var dim = new AngularDimensionEntity(_vertex.Value, _p1.Value, _p2.Value, world);
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
        _vertex = null;
        _p1 = null;
        _p2 = null;
        ClearSnap();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasCursor)
        {
            if (_vertex is { } v && _p1 is null)
                items.Add(new OverlayItem(new LineEntity(v, _cursor), ToolStyle.Preview));
            else if (_vertex is { } vx && _p1 is { } a && _p2 is null)
                items.Add(new OverlayItem(new LineEntity(vx, _cursor), ToolStyle.Preview));
            else if (_vertex is { } vc && _p1 is { } pa && _p2 is { } pb)
            {
                var preview = new AngularDimensionEntity(vc, pa, pb, _cursor);
                CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
                items.Add(new OverlayItem(preview, ToolStyle.Preview));
            }
        }

        AddSnapMarker(items);
        return items;
    }
}
