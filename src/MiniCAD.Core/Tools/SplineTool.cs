using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a smooth spline through clicked fit points. Left-click adds a point; double-click,
/// right-click or Enter finishes it; Escape discards it. A live preview shows the curve through
/// the points placed so far plus the cursor.
/// </summary>
public sealed class SplineTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _current;

    public override string Name => "Spline";

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

        if (input.ClickCount >= 2)
        {
            Finish();
            return;
        }

        Point2D world = Snap(input);
        _points.Add(world);
        _current = world;
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = Snap(input);
        Context.RequestRedraw();
    }

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
            items.Add(new OverlayItem(new SplineEntity(preview), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_points.Count >= 2)
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(new SplineEntity(_points))));

        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }
}
