using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Request raised by the <see cref="TextTool"/> when an inline editor should open: the host
/// shows a text field anchored at <see cref="AnchorWorld"/>, seeds it with
/// <see cref="InitialText"/> and, on confirm, calls <see cref="TextTool.Commit"/>.
/// </summary>
public readonly record struct TextEditRequest(Point2D AnchorWorld, string InitialText, bool Multiline);

/// <summary>
/// Places and edits annotation text. A left click on empty space starts a new text at the
/// snapped cursor; a click on an existing <see cref="TextEntity"/>/<see cref="MTextEntity"/>
/// edits it. The actual typing happens in a host-provided inline editor (the tool stays
/// UI-agnostic and Skia-free): the tool raises <see cref="EditRequested"/>, then the host feeds
/// the result back through <see cref="Commit"/> (creating via <see cref="AddEntityCommand"/> or
/// updating via <see cref="EditEntityCommand"/>) or aborts with <see cref="CancelEdit"/>.
/// </summary>
public sealed class TextTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    // Pending edit session, set between a click (BeginEditAt) and Commit/CancelEdit.
    private bool _sessionActive;
    private Point2D _pendingPosition;
    private IEntity? _editTarget;
    private object? _editBefore;

    /// <summary>Cap height of newly placed text, in world units.</summary>
    public double Height { get; set; } = 12.0;

    public TextHAlign HorizontalAlignment { get; set; } = TextHAlign.Left;

    public TextVAlign VerticalAlignment { get; set; } = TextVAlign.Baseline;

    /// <summary>When true, new text is created as a multi-line <see cref="MTextEntity"/>.</summary>
    public bool Multiline { get; set; }

    public override string Name => "Text";

    /// <summary>Raised when the host should open its inline editor for a new or existing text.</summary>
    public event Action<TextEditRequest>? EditRequested;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        BeginEditAt(Snap(input));
    }

    /// <summary>
    /// Starts an edit session at <paramref name="world"/>: edits the topmost text entity under it
    /// if there is one, otherwise prepares a new text at that point. Raises <see cref="EditRequested"/>.
    /// </summary>
    public void BeginEditAt(Point2D world)
    {
        IEntity? existing = PickText(world);
        if (existing is not null)
        {
            _editTarget = existing;
            _editBefore = ((IEditableEntity)existing).CaptureState();
            _pendingPosition = TextPosition(existing);
            _sessionActive = true;
            EditRequested?.Invoke(new TextEditRequest(_pendingPosition, TextContent(existing), existing is MTextEntity));
        }
        else
        {
            _editTarget = null;
            _editBefore = null;
            _pendingPosition = world;
            _sessionActive = true;
            EditRequested?.Invoke(new TextEditRequest(world, string.Empty, Multiline));
        }

        Context.RequestRedraw();
    }

    /// <summary>
    /// Finishes the current edit session with the typed <paramref name="text"/>. Editing an
    /// existing entity records an <see cref="EditEntityCommand"/>; a new, non-empty text is added
    /// with an <see cref="AddEntityCommand"/>. Returns the affected entity, or <c>null</c> when an
    /// empty new text is discarded. Safe to call standalone (used by tests).
    /// </summary>
    public IEntity? Commit(Point2D position, string text, IEntity? target)
    {
        text ??= string.Empty;

        if (target is not null)
        {
            SetContent(target, text);
            if (_editBefore is { } before && target is IEditableEntity editable)
                Context.Push(new EditEntityCommand(Context.Document, editable, before, editable.CaptureState()));
            else
                Context.Document.NotifyEntityModified(target);

            EndSession();
            return target;
        }

        if (text.Length == 0)
        {
            EndSession();
            return null;
        }

        IEntity created = Multiline
            ? new MTextEntity(position, text, Height, 0.0, 0.0, HorizontalAlignment, TextVAlign.Top)
            : new TextEntity(position, text, Height, 0.0, HorizontalAlignment, VerticalAlignment);

        created = ApplyDefaultStyle(created);
        Context.Execute(new AddEntityCommand(Context.Document, created));
        EndSession();
        return created;
    }

    /// <summary>Commits the pending session captured by <see cref="BeginEditAt"/>.</summary>
    public IEntity? Commit(string text) => Commit(_pendingPosition, text, _editTarget);

    /// <summary>Aborts the pending session without changing the document.</summary>
    public void CancelEdit()
    {
        EndSession();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel() => EndSession();

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);

        // Show a faint placeholder at the cursor while waiting to place a new text.
        if (_hasCursor && !_sessionActive)
        {
            items.Add(new OverlayItem(
                new TextEntity(_cursor, "Text", Height, 0.0, HorizontalAlignment, VerticalAlignment),
                ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void EndSession()
    {
        _sessionActive = false;
        _editTarget = null;
        _editBefore = null;
    }

    private IEntity? PickText(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (entity is not (TextEntity or MTextEntity))
                continue;
            if (Context.Document.IsEntityEditable(entity) && entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }

    private static Point2D TextPosition(IEntity entity) => entity switch
    {
        TextEntity t => t.Position,
        MTextEntity m => m.Position,
        _ => Point2D.Origin,
    };

    private static string TextContent(IEntity entity) => entity switch
    {
        TextEntity t => t.Text,
        MTextEntity m => m.Text,
        _ => string.Empty,
    };

    private static void SetContent(IEntity entity, string text)
    {
        switch (entity)
        {
            case TextEntity t:
                t.Text = text;
                break;
            case MTextEntity m:
                m.Text = text;
                break;
        }
    }
}
