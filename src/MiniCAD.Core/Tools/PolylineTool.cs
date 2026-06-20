using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a multi-segment polyline. Left-click adds vertices; double-click, right-click or
/// Enter finishes it; Escape discards it.
/// </summary>
public sealed class PolylineTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _current;

    public override string Name => "Polylinie";

    protected override bool HasActiveOperation => _points.Count > 0;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Finish();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        // The second press of a double-click finishes instead of adding a duplicate vertex.
        if (input.ClickCount >= 2)
        {
            Finish();
            return;
        }

        Point2D world = NextPoint(input);
        _points.Add(world);
        _current = world;
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = NextPoint(input);
        Context.RequestRedraw();
    }

    // From the second vertex on, Shift locks the new segment to an ortho/diagonal direction.
    private Point2D NextPoint(in ToolPointerInput input)
        => _points.Count > 0 ? ResolveSegmentPoint(_points[^1], input) : Snap(input);

    public override void KeyDown(ToolKey key)
    {
        if (key == ToolKey.Enter)
        {
            Finish();
            return;
        }

        base.KeyDown(key);
    }

    public override void Cancel()
    {
        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_points.Count > 0)
        {
            var preview = new List<Point2D>(_points) { _current };
            items.Add(new OverlayItem(new PolylineEntity(preview), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_points.Count >= 2)
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(new PolylineEntity(_points))));

        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }
}
