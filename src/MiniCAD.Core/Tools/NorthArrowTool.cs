using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>Places a north-arrow symbol: click sets the center, a second click sets the direction (#213).</summary>
public sealed class NorthArrowTool : ToolBase
{
    private bool _hasCenter;
    private Point2D _center;
    private Point2D _current;

    public double Size { get; set; } = 60.0;

    public override string Name => "Nordpfeil";

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
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(Build(world))));
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
            items.Add(new OverlayItem(Build(_current), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }

    private NorthArrowEntity Build(Point2D pointer)
    {
        Vector2D d = pointer - _center;
        double direction = d.Length > GeometryMath.Epsilon ? Math.Atan2(d.Y, d.X) : Math.PI / 2.0;
        return new NorthArrowEntity(_center, Size, direction);
    }
}
