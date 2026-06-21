using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places a linear/aligned dimension: click the first point, the second point, then a point the
/// dimension line runs through (its offset/side). The measurement is computed automatically and
/// the active <see cref="DimStyle"/> is applied. Object snapping helps hit exact endpoints.
/// </summary>
public sealed class LinearDimensionTool : ToolBase
{
    private Point2D? _p1;
    private Point2D? _p2;
    private Point2D _cursor;
    private bool _hasCursor;

    /// <summary>Aligned (parallel), horizontal or vertical dimension line.</summary>
    public LinearDimensionKind Kind { get; set; } = LinearDimensionKind.Aligned;

    public override string Name => "Maß";

    protected override bool HasActiveOperation => _p1 is not null;

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
        if (_p1 is null)
        {
            _p1 = world;
        }
        else if (_p2 is null)
        {
            _p2 = world;
        }
        else
        {
            var dim = new LinearDimensionEntity(_p1.Value, _p2.Value, world, Kind);
            CadDocument.ApplyDimStyle(dim, Context.Document.ActiveDimStyle);
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(dim)));
            Reset();
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel() => Reset();

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);

        if (_p1 is { } p1 && _p2 is null && _hasCursor)
        {
            items.Add(new OverlayItem(new LineEntity(p1, _cursor), ToolStyle.Preview));
        }
        else if (_p1 is { } a && _p2 is { } b && _hasCursor)
        {
            var preview = new LinearDimensionEntity(a, b, _cursor, Kind);
            CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Reset()
    {
        _p1 = null;
        _p2 = null;
        ClearSnap();
    }
}
