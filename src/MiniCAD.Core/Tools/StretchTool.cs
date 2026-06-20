using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Stretches geometry: drag a crossing window over the vertices to move, then give a base and a
/// target point. Only the vertices captured inside the window travel by the displacement — the
/// rest of each object stays put, so a rectangle's corner can be pulled while the far side holds.
/// Objects fully enclosed move as a whole. One undoable step; the tool stays active afterwards.
/// </summary>
public sealed class StretchTool : ToolBase
{
    private readonly record struct Affected(IEntity Entity, IReadOnlyList<int> VertexIndices, bool Whole);

    private bool _hasFirstCorner;
    private Point2D _firstCorner;
    private Point2D _secondCorner;

    private bool _windowDone;
    private Rect2D _window;
    private List<Affected> _affected = new();

    private bool _hasBase;
    private Point2D _base;
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Dehnen";

    protected override bool HasActiveOperation => _hasFirstCorner || _windowDone;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (!_windowDone)
        {
            if (!_hasFirstCorner)
            {
                _firstCorner = input.World;
                _secondCorner = input.World;
                _hasFirstCorner = true;
            }
            else
            {
                _secondCorner = input.World;
                FinalizeWindow();
            }
        }
        else if (!_hasBase)
        {
            _base = Snap(input);
            _cursor = _base;
            _hasBase = true;
        }
        else
        {
            Commit(ResolveSegmentPoint(_base, input) - _base);
            ResetGesture();
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        if (!_windowDone)
            _secondCorner = input.World;
        else
            _cursor = _hasBase ? ResolveSegmentPoint(_base, input) : Snap(input);

        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        ResetGesture();
        Context.RequestRedraw();
    }

    private void FinalizeWindow()
    {
        _window = Rect2D.FromPoints(_firstCorner, _secondCorner);
        _affected = CollectAffected(_window);
        _windowDone = true;
    }

    private List<Affected> CollectAffected(Rect2D window)
    {
        var affected = new List<Affected>();
        foreach (IEntity entity in Context.Document.Entities)
        {
            if (!Context.Document.IsEntityEditable(entity))
                continue;

            if (entity is IEditableEntity editable)
            {
                var indices = new List<int>();
                foreach (Grip grip in editable.GetGrips())
                {
                    if (grip.Kind == GripKind.Vertex && window.Contains(grip.Position))
                        indices.Add(grip.Index);
                }

                if (indices.Count > 0)
                    affected.Add(new Affected(entity, indices, Whole: false));
            }
            else if (window.Contains(entity.Bounds))
            {
                affected.Add(new Affected(entity, Array.Empty<int>(), Whole: true));
            }
        }

        return affected;
    }

    private void Commit(Vector2D delta)
    {
        if (delta == Vector2D.Zero || _affected.Count == 0)
            return;

        var commands = new List<IUndoableCommand>(_affected.Count);
        foreach (Affected item in _affected)
        {
            if (item.Whole || item.Entity is not IEditableEntity editable)
            {
                commands.Add(new TransformEntitiesCommand(
                    Context.Document, new[] { item.Entity }, Matrix2D.Translation(delta)));
                continue;
            }

            object before = editable.CaptureState();
            StretchVertices(editable, item.VertexIndices, delta);
            object after = editable.CaptureState();
            editable.RestoreState(before); // revert; the command re-applies on Execute
            commands.Add(new EditEntityCommand(Context.Document, editable, before, after));
        }

        Context.Execute(new CompositeCommand("Dehnen", commands));
    }

    private static void StretchVertices(IEditableEntity editable, IReadOnlyList<int> indices, Vector2D delta)
    {
        IReadOnlyList<Grip> grips = editable.GetGrips();
        foreach (int index in indices)
        {
            foreach (Grip grip in grips)
            {
                if (grip.Index == index)
                {
                    editable.MoveGrip(grip, grip.Position + delta);
                    break;
                }
            }
        }
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();

        if (!_windowDone && _hasFirstCorner)
        {
            Rect2D box = Rect2D.FromPoints(_firstCorner, _secondCorner);
            items.Add(new OverlayItem(PolylineEntity.Rectangle(box.Min, box.Max), ToolStyle.CrossingBox));
        }
        else if (_windowDone)
        {
            items.Add(new OverlayItem(PolylineEntity.Rectangle(_window.Min, _window.Max), ToolStyle.CrossingBox));

            if (_hasBase && _hasCursor)
            {
                Vector2D delta = _cursor - _base;
                if (delta != Vector2D.Zero)
                {
                    foreach (Affected item in _affected)
                    {
                        IEntity preview = item.Entity.Clone();
                        if (item.Whole || preview is not IEditableEntity editable)
                            preview.Transform(Matrix2D.Translation(delta));
                        else
                            StretchVertices(editable, item.VertexIndices, delta);
                        items.Add(new OverlayItem(preview, ToolStyle.Preview));
                    }
                }

                items.Add(new OverlayItem(new LineEntity(_base, _cursor), ToolStyle.Preview));
            }
        }

        AddSnapMarker(items);
        return items;
    }

    private void ResetGesture()
    {
        _hasFirstCorner = false;
        _windowDone = false;
        _hasBase = false;
        _hasCursor = false;
        _affected = new List<Affected>();
        ClearSnap();
    }
}
