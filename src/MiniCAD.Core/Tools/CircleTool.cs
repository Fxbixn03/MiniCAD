using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Draws a circle: click the center, then click a point on the radius.</summary>
public sealed class CircleTool : ToolBase
{
    private bool _hasCenter;
    private Point2D _center;
    private Point2D _current;

    public override string Name => "Kreis";

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
            double radius = _center.DistanceTo(world);
            if (radius > GeometryMath.Epsilon)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(new CircleEntity(_center, radius))));
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
        if (_hasCenter)
            items.Add(new OverlayItem(new CircleEntity(_center, _center.DistanceTo(_current)), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }
}
