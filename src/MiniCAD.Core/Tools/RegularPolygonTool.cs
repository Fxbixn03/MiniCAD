using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a regular polygon (#196): click the center, then a point that sets the size and
/// orientation. <see cref="Sides"/> and <see cref="Circumscribed"/> come from the inline options.
/// </summary>
public sealed class RegularPolygonTool : ToolBase
{
    private bool _hasCenter;
    private Point2D _center;
    private Point2D _current;

    /// <summary>Number of sides (clamped to at least 3 by the factory).</summary>
    public int Sides { get; set; } = 6;

    /// <summary>When true the polygon is circumscribed about the radius circle (edges tangent).</summary>
    public bool Circumscribed { get; set; }

    public override string Name => "Vieleck";

    protected override bool HasActiveOperation => _hasCenter;

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
        if (!_hasCenter)
        {
            _center = world;
            _current = world;
            _hasCenter = true;
        }
        else
        {
            if (Build(world) is { } polygon)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(polygon)));
            _hasCenter = false;
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
        _hasCenter = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasCenter && Build(_current) is { } preview)
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }

    private PolylineEntity? Build(Point2D pointer)
    {
        double radius = _center.DistanceTo(pointer);
        if (radius <= GeometryMath.Epsilon)
            return null;

        double angle = Math.Atan2(pointer.Y - _center.Y, pointer.X - _center.X);
        return RegularPolygonFactory.Create(_center, radius, Sides, Circumscribed, angle);
    }
}
