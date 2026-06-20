using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Draws an axis-aligned rectangle from two opposite corners (two clicks).</summary>
public sealed class RectangleTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public override string Name => "Rechteck";

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

        Point2D world = Snap(input);
        if (!_hasStart)
        {
            _start = world;
            _current = world;
            _hasStart = true;
        }
        else
        {
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(BuildRectangle(_start, world))));
            _hasStart = false;
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = Snap(input);
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
            items.Add(new OverlayItem(BuildRectangle(_start, _current), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }

    private static PolylineEntity BuildRectangle(Point2D a, Point2D b)
    {
        Rect2D rect = Rect2D.FromPoints(a, b);
        return PolylineEntity.Rectangle(rect.Min, rect.Max);
    }
}
