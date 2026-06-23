using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a double/parallel line run (Allplan "Doppellinie", #236): vertices are placed like a
/// polyline; on finish two parallel, mitered polylines are created at <see cref="Width"/> with the
/// chosen <see cref="Justification"/>. The two rails are grouped so they move as one.
/// </summary>
public sealed class MultilineTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _current;

    public double Width { get; set; } = 240.0;

    public MultilineJustification Justification { get; set; } = MultilineJustification.Center;

    public override string Name => "Doppellinie";

    protected override bool HasActiveOperation => _points.Count > 0;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Finish();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (input.ClickCount >= 2)
        {
            Finish();
            return;
        }

        Point2D world = NextPoint(input);
        _points.Add(world);
        _current = world;
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = NextPoint(input);
        Context.RequestRedraw();
    }

    private Point2D NextPoint(in ToolPointerInput input)
        => _points.Count > 0 ? ResolveSegmentPoint(_points[^1], input) : Snap(input);

    public override void KeyDown(ToolKey key)
    {
        if (key == ToolKey.Enter)
        {
            Finish();
            return;
        }

        base.KeyDown(key);
    }

    public override void Cancel()
    {
        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(3);
        if (_points.Count > 0)
        {
            var preview = new List<Point2D>(_points) { _current };
            foreach (PolylineEntity rail in MultilineGenerator.Generate(preview, closed: false, Width, Justification))
                items.Add(new OverlayItem(rail, ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_points.Count >= 2)
        {
            Guid group = Guid.NewGuid();
            var commands = new List<IUndoableCommand>();
            foreach (PolylineEntity rail in MultilineGenerator.Generate(_points, closed: false, Width, Justification))
            {
                PolylineEntity styled = ApplyDefaultStyle(rail);
                styled.GroupId = group;
                commands.Add(new AddEntityCommand(Context.Document, styled));
            }

            Context.Execute(new CompositeCommand("Doppellinie", commands));
        }

        _points.Clear();
        ClearSnap();
        Context.RequestRedraw();
    }
}
