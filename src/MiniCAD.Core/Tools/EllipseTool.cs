using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a full ellipse from three clicks: centre, the endpoint of one axis (sets that radius and
/// the rotation), then a point giving the other axis's half-length. Live preview, object snapping
/// and <see cref="AddEntityCommand"/>.
/// </summary>
public sealed class EllipseTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Ellipse";

    protected override bool HasActiveOperation => _points.Count > 0;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        _points.Add(Snap(input));
        if (_points.Count >= 3)
        {
            if (Build(_points[0], _points[1], _points[2]) is { } ellipse)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(ellipse)));
            _points.Clear();
            ClearSnap();
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_hasCursor && _points.Count >= 1)
        {
            if (_points.Count >= 2 && Build(_points[0], _points[1], _cursor) is { } ellipse)
                items.Add(new OverlayItem(ellipse, ToolStyle.Preview));
            else
                items.Add(new OverlayItem(new LineEntity(_points[0], _cursor), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private static EllipseEntity? Build(Point2D center, Point2D axisEnd, Point2D minorPoint)
    {
        double radiusX = center.DistanceTo(axisEnd);
        if (radiusX <= GeometryMath.Epsilon)
            return null;

        double rotation = Math.Atan2(axisEnd.Y - center.Y, axisEnd.X - center.X);
        var minorDirection = new Vector2D(-Math.Sin(rotation), Math.Cos(rotation));
        double radiusY = Math.Abs((minorPoint - center).Dot(minorDirection));
        if (radiusY <= GeometryMath.Epsilon)
            return null;

        return new EllipseEntity(center, radiusX, radiusY, rotation);
    }
}
