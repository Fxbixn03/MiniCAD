using System.Linq;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// The idle/default tool. Picks entities (Shift to extend the selection) and moves them by
/// dragging. When a single editable entity is selected it also shows grips: dragging a vertex
/// grip moves that point, dragging an edge grip slides the whole side (so a rectangle's side
/// can be pulled without moving the opposite side). Moves and grip edits are applied live and
/// recorded as one undoable step on release. Delete removes the selection.
/// </summary>
public sealed class SelectTool : ToolBase
{
    private bool _dragging;
    private Point2D _lastWorld;
    private Vector2D _totalDelta;
    private List<IEntity> _dragTargets = new();

    // Grip editing of a single selected entity.
    private IEditableEntity? _gripEntity;
    private Grip _activeGrip;
    private object? _gripBefore;

    // Rubber-band box selection (started on empty space).
    private bool _boxing;
    private Point2D _boxStart;
    private Point2D _boxCurrent;

    public override string Name => "Auswahl";

    protected override bool HasActiveOperation => _dragging || _gripEntity is not null || _boxing;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button != ToolButton.Left)
            return;

        // Grip editing takes precedence: if a single editable entity is selected and the
        // press lands on one of its grips, start a grip drag instead of a pick/move.
        if (TryStartGripDrag(input.World))
            return;

        IEntity? hit = PickTopmost(input.World);
        if (hit is null)
        {
            // Empty space: begin a rubber-band box. A drag selects by window/crossing; a click
            // without a drag clears the selection (resolved on release).
            _boxing = true;
            _boxStart = input.World;
            _boxCurrent = input.World;
            return;
        }

        IReadOnlyList<IEntity> group = ResolveGroup(hit);
        if (input.HasShift)
        {
            foreach (IEntity member in group)
                Context.Selection.Toggle(member);
        }
        else if (!Context.Selection.Contains(hit))
        {
            Context.Selection.Set(group);
        }

        if (Context.Selection.Contains(hit))
        {
            _dragging = true;
            _lastWorld = input.World;
            _totalDelta = Vector2D.Zero;
            _dragTargets = new List<IEntity>(Context.Selection.Items);
        }
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        if (_boxing)
        {
            _boxCurrent = input.World;
            Context.RequestRedraw();
            return;
        }

        if (_gripEntity is { } gripEntity)
        {
            gripEntity.MoveGrip(_activeGrip, Snap(input));
            Context.Document.NotifyEntityModified((IEntity)gripEntity);
            Context.RequestRedraw();
            return;
        }

        if (!_dragging)
            return;

        Vector2D delta = input.World - _lastWorld;
        if (delta == Vector2D.Zero)
            return;

        var move = Matrix2D.Translation(delta);
        foreach (IEntity entity in _dragTargets)
        {
            entity.Transform(move);
            Context.Document.NotifyEntityModified(entity);
        }

        _totalDelta += delta;
        _lastWorld = input.World;
    }

    public override void PointerUp(in ToolPointerInput input)
    {
        if (_boxing)
        {
            FinishBox(input.HasShift);
            return;
        }

        if (_gripEntity is { } gripEntity)
        {
            object after = gripEntity.CaptureState();
            // Record the edit (already applied) so undo can revert it.
            if (_gripBefore is { } before)
                Context.Push(new EditEntityCommand(Context.Document, gripEntity, before, after));

            _gripEntity = null;
            _gripBefore = null;
            ClearSnap();
            Context.RequestRedraw();
            return;
        }

        if (!_dragging)
            return;

        _dragging = false;
        if (_totalDelta.LengthSquared > 1e-12)
        {
            // The move is already applied; record the net translation so undo can reverse it.
            Context.Push(new TransformEntitiesCommand(
                Context.Document, _dragTargets, Matrix2D.Translation(_totalDelta)));
        }

        _dragTargets = new List<IEntity>();
    }

    public override void KeyDown(ToolKey key)
    {
        if (key == ToolKey.Delete)
        {
            DeleteSelection();
            return;
        }

        base.KeyDown(key);
    }

    protected override void OnEscapeIdle() => Context.Selection.Clear();

    public override void Cancel()
    {
        // Abort an in-progress grip edit cleanly, restoring the pre-drag geometry.
        if (_gripEntity is { } gripEntity && _gripBefore is { } before)
        {
            gripEntity.RestoreState(before);
            Context.Document.NotifyEntityModified((IEntity)gripEntity);
        }

        _gripEntity = null;
        _gripBefore = null;
        _dragging = false;
        _boxing = false;
        _dragTargets = new List<IEntity>();
        ClearSnap();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();

        if (_boxing)
        {
            Rect2D box = Rect2D.FromPoints(_boxStart, _boxCurrent);
            var marker = PolylineEntity.Rectangle(box.Min, box.Max);
            items.Add(new OverlayItem(marker, IsCrossingDrag ? ToolStyle.CrossingBox : ToolStyle.WindowBox));
        }

        if (SingleEditableSelection() is { } editable)
        {
            foreach (Grip grip in editable.GetGrips())
                AddGripMarker(items, grip);
        }

        AddSnapMarker(items);
        return items;
    }

    /// <summary>True while the box is dragged right→left, i.e. a crossing (intersecting) selection.</summary>
    private bool IsCrossingDrag => _boxCurrent.X < _boxStart.X;

    private void FinishBox(bool additive)
    {
        _boxing = false;

        // A box smaller than the pick tolerance is really a click on empty space.
        double slack = Context.PickTolerance;
        if (Math.Abs(_boxCurrent.X - _boxStart.X) < slack && Math.Abs(_boxCurrent.Y - _boxStart.Y) < slack)
        {
            if (!additive)
                Context.Selection.Clear();
            Context.RequestRedraw();
            return;
        }

        bool crossing = IsCrossingDrag;
        Rect2D box = Rect2D.FromPoints(_boxStart, _boxCurrent);

        if (!additive)
            Context.Selection.Clear();

        foreach (IEntity entity in Context.Document.Entities)
        {
            if (!Context.Document.IsEntityEditable(entity))
                continue;

            // Window (left→right): only fully enclosed objects. Crossing (right→left): also cut.
            bool selected = crossing ? entity.IntersectsRect(box) : box.Contains(entity.Bounds);
            if (selected)
                Context.Selection.Add(entity);
        }

        ExpandSelectionToGroups();
        Context.RequestRedraw();
    }

    /// <summary>Pulls in every member of any group a selected entity belongs to.</summary>
    private void ExpandSelectionToGroups()
    {
        var groupIds = Context.Selection.Items
            .Select(e => e.GroupId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (Guid groupId in groupIds)
        {
            foreach (IEntity entity in Context.Document.Entities)
            {
                if (entity.GroupId == groupId && Context.Document.IsEntityEditable(entity))
                    Context.Selection.Add(entity);
            }
        }
    }

    /// <summary>The hit entity plus every other entity sharing its (non-empty) group id.</summary>
    private IReadOnlyList<IEntity> ResolveGroup(IEntity hit)
    {
        if (hit.GroupId == Guid.Empty)
            return new[] { hit };

        return Context.Document.Entities
            .Where(e => e.GroupId == hit.GroupId && Context.Document.IsEntityEditable(e))
            .ToList();
    }

    private bool TryStartGripDrag(Point2D world)
    {
        if (SingleEditableSelection() is not { } editable)
            return false;

        double tolerance = Context.PickTolerance * 1.8;
        double bestSquared = tolerance * tolerance;
        Grip? best = null;
        foreach (Grip grip in editable.GetGrips())
        {
            double distanceSquared = world.DistanceSquaredTo(grip.Position);
            if (distanceSquared <= bestSquared)
            {
                bestSquared = distanceSquared;
                best = grip;
            }
        }

        if (best is not { } chosen)
            return false;

        _gripEntity = editable;
        _activeGrip = chosen;
        _gripBefore = editable.CaptureState();
        return true;
    }

    /// <summary>The selection iff it is exactly one editable, editable-state entity.</summary>
    private IEditableEntity? SingleEditableSelection()
    {
        if (Context.Selection.Count != 1)
            return null;

        IEntity entity = Context.Selection.Items[0];
        if (!Context.Document.IsEntityEditable(entity))
            return null;

        return entity as IEditableEntity;
    }

    private void AddGripMarker(List<OverlayItem> items, Grip grip)
    {
        double half = Context.PickTolerance * 1.4;
        Point2D p = grip.Position;
        var marker = PolylineEntity.Rectangle(
            new Point2D(p.X - half, p.Y - half),
            new Point2D(p.X + half, p.Y + half));
        var style = grip.Kind == GripKind.Vertex ? ToolStyle.VertexGrip : ToolStyle.EdgeGrip;
        items.Add(new OverlayItem(marker, style));
    }

    private void DeleteSelection()
    {
        if (Context.Selection.IsEmpty)
            return;

        var removals = Context.Selection.Items
            .Select(entity => (IUndoableCommand)new RemoveEntityCommand(Context.Document, entity))
            .ToList();

        Context.Execute(new CompositeCommand("Löschen", removals));
        Context.Selection.Clear();
    }

    private IEntity? PickTopmost(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;

        // Iterate back-to-front so the visually topmost entity wins. Only editable entities
        // (Active layer and Active Teilbild) can be picked for selection/editing.
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (!Context.Document.IsEntityEditable(entity))
                continue;

            if (entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }
}
