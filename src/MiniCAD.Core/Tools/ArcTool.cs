using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>The two ways of constructing an arc.</summary>
public enum ArcMode
{
    /// <summary>Start, a point on the arc, then end.</summary>
    ThreePoint,

    /// <summary>Centre, start point (sets radius), then end direction (CCW).</summary>
    CenterStartEnd,
}

/// <summary>
/// Draws an <see cref="ArcEntity"/> in one of two modes (see <see cref="ArcMode"/>), each a
/// three-click gesture with a live preview. The mode comes from the inline options bar.
/// </summary>
public sealed class ArcTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _cursor;
    private bool _hasCursor;

    public ArcMode Mode { get; set; } = ArcMode.ThreePoint;

    public override string Name => "Bogen";

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
            if (BuildArc(_points[0], _points[1], _points[2]) is { } arc)
                Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(arc)));
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
            if (_points.Count >= 2 && BuildArc(_points[0], _points[1], _cursor) is { } arc)
                items.Add(new OverlayItem(arc, ToolStyle.Preview));
            else
                items.Add(new OverlayItem(new LineEntity(_points[^1], _cursor), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private ArcEntity? BuildArc(Point2D a, Point2D b, Point2D c) => Mode switch
    {
        ArcMode.CenterStartEnd => ArcEntity.FromCenterStartEnd(a, b, c),
        _ => ArcEntity.FromThreePoints(a, b, c),
    };
}
