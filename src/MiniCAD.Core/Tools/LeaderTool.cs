using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a leader/callout: left-click sets the arrow tip, then each further click adds a bend
/// point. Right-click, double-click or Enter finishes the line (needs at least two points) and
/// opens the inline editor for the label; confirming creates the <see cref="LeaderEntity"/>.
/// </summary>
public sealed class LeaderTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private Point2D _current;
    private bool _awaitingText;

    /// <summary>Cap height of the leader's label, in world units.</summary>
    public double TextHeight { get; set; } = 12.0;

    public override string Name => "Führungslinie";

    /// <summary>Raised when the line is finished and the label should be typed.</summary>
    public event Action<TextEditRequest>? EditRequested;

    protected override bool HasActiveOperation => _points.Count > 0 || _awaitingText;

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

    // The first point snaps freely; later segments honour ortho/polar (and Shift).
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
        _awaitingText = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_points.Count > 0 && !_awaitingText)
        {
            var preview = new List<Point2D>(_points) { _current };
            items.Add(new OverlayItem(new PolylineEntity(preview), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_points.Count < 2)
        {
            Cancel();
            return;
        }

        // Keep the points until the label is entered; the editor drives the final commit.
        _awaitingText = true;
        ClearSnap();
        EditRequested?.Invoke(new TextEditRequest(
            _points[^1], string.Empty, multiline: false, CommitLabel, CancelLabel));
        Context.RequestRedraw();
    }

    private void CommitLabel(string text)
    {
        if (_points.Count >= 2)
        {
            var leader = new LeaderEntity(_points, text ?? string.Empty, TextHeight, TextHeight);
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(leader)));
        }

        _points.Clear();
        _awaitingText = false;
        Context.RequestRedraw();
    }

    private void CancelLabel()
    {
        _points.Clear();
        _awaitingText = false;
        Context.RequestRedraw();
    }
}
