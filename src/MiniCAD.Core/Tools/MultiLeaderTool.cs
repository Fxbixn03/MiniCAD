using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Draws a multi-leader (#239): each left-click adds an arrow tip; right-click/double-click/Enter
/// finishes, using the current cursor as the shared label landing and opening the inline editor for
/// the label text.
/// </summary>
public sealed class MultiLeaderTool : ToolBase
{
    private readonly List<Point2D> _tips = new();
    private Point2D _current;
    private Point2D _landing;
    private bool _awaitingText;

    public double TextHeight { get; set; } = 12.0;

    public override string Name => "Multi-Führungslinie";

    /// <summary>Raised when the leaders are placed and the label should be typed.</summary>
    public event Action<TextEditRequest>? EditRequested;

    protected override bool HasActiveOperation => _tips.Count > 0 || _awaitingText;

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

        Point2D world = Snap(input);
        _tips.Add(world);
        _current = world;
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _current = Snap(input);
        Context.RequestRedraw();
    }

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
        _tips.Clear();
        _awaitingText = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_tips.Count > 0 && !_awaitingText)
            items.Add(new OverlayItem(new MultiLeaderEntity(_current, _tips, string.Empty, TextHeight, TextHeight), ToolStyle.Preview));
        AddSnapMarker(items);
        return items;
    }

    private void Finish()
    {
        if (_tips.Count < 1)
        {
            Cancel();
            return;
        }

        _landing = _current;
        _awaitingText = true;
        ClearSnap();
        EditRequested?.Invoke(new TextEditRequest(_landing, string.Empty, multiline: false, CommitLabel, CancelLabel));
        Context.RequestRedraw();
    }

    private void CommitLabel(string text)
    {
        if (_tips.Count >= 1)
        {
            var leader = new MultiLeaderEntity(_landing, _tips, text ?? string.Empty, TextHeight, TextHeight);
            Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(leader)));
        }

        _tips.Clear();
        _awaitingText = false;
        Context.RequestRedraw();
    }

    private void CancelLabel()
    {
        _tips.Clear();
        _awaitingText = false;
        Context.RequestRedraw();
    }
}
