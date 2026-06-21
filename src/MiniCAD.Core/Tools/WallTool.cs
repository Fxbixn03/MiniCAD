using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a wall along its axis (#73): click the start of the centerline, then click the end. The
/// thickness, height and base elevation come from the tool's settings (the inline options line);
/// each finished wall is one undoable step and feeds the live 3D model.
/// </summary>
public sealed class WallTool : ToolBase
{
    private bool _hasStart;
    private Point2D _start;
    private Point2D _current;

    public double Thickness { get; set; } = 240.0;
    public double Height { get; set; } = 2500.0;
    public double BaseElevation { get; set; }

    /// <summary>Which line is drawn (Anschlag): centerline, or a left/right face.</summary>
    public DrawReference Reference { get; set; } = DrawReference.Center;

    public override string Name => "Wand";

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
            {
                Vector2D off = DrawReferenceMath.CenterlineOffset(_start, end, Thickness, Reference);
                Context.Execute(new AddEntityCommand(Context.Document,
                    ApplyDefaultStyle(new WallEntity(_start + off, end + off, Thickness, Height, BaseElevation))));
            }
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
        {
            Vector2D off = DrawReferenceMath.CenterlineOffset(_start, _current, Thickness, Reference);
            items.Add(new OverlayItem(new WallEntity(_start + off, _current + off, Thickness, Height, BaseElevation), ToolStyle.Preview));
        }
        AddSnapMarker(items);
        return items;
    }
}
