using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws an infinite construction line or a ray (#195): click the base point, then a point that
/// sets the direction. <see cref="Ray"/> chooses between a both-ways line and a half-infinite ray.
/// </summary>
public sealed class ConstructionLineTool : ToolBase
{
    private bool _hasBase;
    private Point2D _base;
    private Point2D _current;

    /// <summary>When true the tool draws a half-infinite ray instead of a both-ways construction line.</summary>
    public bool Ray { get; set; }

    public override string Name => "Konstruktionslinie";

    protected override bool HasActiveOperation => _hasBase;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        Point2D world = _hasBase ? ResolveSegmentPoint(_base, input) : Snap(input);
        if (!_hasBase)
        {
            _base = world;
            _current = world;
            _hasBase = true;
        }
        else
        {
            if (Build(world) is { } entity)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(entity)));
            _hasBase = false;
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = _hasBase ? ResolveSegmentPoint(_base, input) : Snap(input);
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _hasBase = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasBase && Build(_current) is { } preview)
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }

    private InfiniteLineEntity? Build(Point2D pointer)
    {
        Vector2D d = pointer - _base;
        if (d.Length <= GeometryMath.Epsilon)
            return null;

        double angle = Math.Atan2(d.Y, d.X);
        return Ray ? new RayEntity(_base, angle) : new ConstructionLineEntity(_base, angle);
    }
}
