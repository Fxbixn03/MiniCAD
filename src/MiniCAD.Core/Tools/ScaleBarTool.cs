using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Draws a scale bar: click the start, then the end (length and direction) (#213).</summary>
public sealed class ScaleBarTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public int Divisions { get; set; } = 4;

    public double Height { get; set; } = 20.0;

    public override string Name => "Maßstabsbalken";

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

        Point2D world = _hasStart ? ResolveSegmentPoint(_start, input) : Snap(input);
        if (!_hasStart)
        {
            _start = world;
            _current = world;
            _hasStart = true;
        }
        else
        {
            if (_start.DistanceTo(world) > GeometryMath.Epsilon)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(
                    new ScaleBarEntity(_start, world, Divisions, Height))));
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
        if (_hasStart && _start.DistanceTo(_current) > GeometryMath.Epsilon)
            items.Add(new OverlayItem(new ScaleBarEntity(_start, _current, Divisions, Height), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
