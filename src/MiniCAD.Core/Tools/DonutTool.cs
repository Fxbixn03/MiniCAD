using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a donut/ring (#196): click the center, then a point on the outer ring. The inner radius
/// comes from the inline options. The two circles are grouped so they move as one.
/// </summary>
public sealed class DonutTool : ToolBase
{
    private bool _hasCenter;
    private Point2D _center;
    private Point2D _current;

    /// <summary>Inner radius in world units.</summary>
    public double InnerRadius { get; set; } = 5.0;

    public override string Name => "Donut";

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
            double outer = _center.DistanceTo(world);
            if (outer > GeometryMath.Epsilon)
            {
                Guid group = Guid.NewGuid();
                var commands = new List<IUndoableCommand>();
                foreach (CircleEntity circle in DonutFactory.Create(_center, InnerRadius, outer))
                {
                    CircleEntity styled = ApplyDefaultStyle(circle);
                    styled.GroupId = group;
                    commands.Add(new AddEntityCommand(Context.Document, styled));
                }

                Context.Execute(new CompositeCommand("Donut", commands));
            }

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
        var items = new List<OverlayItem>(3);
        if (_hasCenter)
        {
            double outer = _center.DistanceTo(_current);
            if (outer > GeometryMath.Epsilon)
            {
                foreach (CircleEntity circle in DonutFactory.Create(_center, InnerRadius, outer))
                    items.Add(new OverlayItem(circle, ToolStyle.Preview));
            }
        }

        AddSnapMarker(items);
        return items;
    }
}
