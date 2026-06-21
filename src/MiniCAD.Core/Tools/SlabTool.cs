using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a slab (Decke) by its outline: left-click adds boundary points, double-click / right-click /
/// Enter closes it. Thickness and base elevation come from the tool settings; the closed outline
/// becomes a live 3D slab.
/// </summary>
public sealed class SlabTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _current;

    public double Thickness { get; set; } = 200.0;
    public double BaseElevation { get; set; }

    public override string Name => "Decke";

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
            items.Add(new OverlayItem(new PolylineEntity(preview, isClosed: _points.Count >= 2), ToolStyle.Preview));
        }
        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_points.Count >= 3)
            Context.Execute(new AddEntityCommand(Context.Document,
                ApplyDefaultStyle(new SlabEntity(_points, Thickness, BaseElevation))));

        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }
}
