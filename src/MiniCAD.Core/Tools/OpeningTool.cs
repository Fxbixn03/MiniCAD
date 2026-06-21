using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a recess/opening (Aussparung, #143): click the axis start, then the end. Width, height and
/// base elevation come from the tool settings; the result subtracts material from any wall it
/// overlaps in the live 3D model.
/// </summary>
public sealed class OpeningTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public double Width { get; set; } = 500.0;
    public double Height { get; set; } = 2100.0;
    public double BaseElevation { get; set; }

    public override string Name => "Aussparung";

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
                    ApplyDefaultStyle(new OpeningEntity(_start, end, Width, Height, BaseElevation))));
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
            items.Add(new OverlayItem(new OpeningEntity(_start, _current, Width, Height, BaseElevation), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
