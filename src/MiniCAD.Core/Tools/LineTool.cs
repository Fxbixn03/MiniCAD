using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Draws a line segment: click the start point, then click the end point.</summary>
public sealed class LineTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public override string Name => "Linie";

    protected override bool HasActiveOperation => _hasStart;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (!_hasStart)
        {
            Point2D start = Snap(input);
            _start = start;
            _current = start;
            _hasStart = true;
        }
        else
        {
            Point2D end = ResolveSegmentPoint(_start, input);
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(new LineEntity(_start, end))));
            _hasStart = false;
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = _hasStart ? ResolveSegmentPoint(_start, input) : Snap(input);
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _hasStart = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasStart)
            items.Add(new OverlayItem(new LineEntity(_start, _current), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
