using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Draws a beam/downstand (Unterzug, #144) along its axis: click start, then end.</summary>
public sealed class BeamTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public double Width { get; set; } = 240.0;
    public double Height { get; set; } = 400.0;
    public double BaseElevation { get; set; } = 2500.0;

    public override string Name => "Unterzug";

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
            _start = Snap(input);
            _current = _start;
            _hasStart = true;
        }
        else
        {
            Point2D end = ResolveSegmentPoint(_start, input);
            if (end.DistanceTo(_start) > GeometryMath.Epsilon)
                Context.Execute(new AddEntityCommand(Context.Document,
                    ApplyDefaultStyle(new BeamEntity(_start, end, Width, Height, BaseElevation))));
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
            items.Add(new OverlayItem(new BeamEntity(_start, _current, Width, Height, BaseElevation), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
