using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Shared base for the editing tools that apply an affine transform to the current selection
/// (move, copy, rotate, mirror, scale). The user selects entities first, then activates the
/// tool, which snapshots the selection and walks a small click sequence (base point, then one
/// or two reference points). A live preview of the transformed clones is drawn while the
/// gesture is in progress, and the result is committed as a single undoable step:
/// <see cref="TransformEntitiesCommand"/> for an in-place edit, or a batch of
/// <see cref="AddEntityCommand"/>s when <see cref="Copy"/> leaves the originals untouched.
/// </summary>
public abstract class SelectionTransformTool : ToolBase
{
    private readonly List<Point2D> _points = new();
    private List<IEntity> _targets = new();
    private Point2D _cursor;
    private bool _hasCursor;

    protected SelectionTransformTool(bool copy = false) => Copy = copy;

    /// <summary>When true the transform is applied to fresh clones, leaving the originals in place.</summary>
    protected bool Copy { get; }

    /// <summary>Number of clicks the gesture needs (e.g. base + target = 2, scale-by-reference = 3).</summary>
    protected abstract int RequiredPoints { get; }

    /// <summary>The first confirmed point of the gesture, if any (typically the base point).</summary>
    protected Point2D? FirstPoint => _points.Count > 0 ? _points[0] : null;

    protected override bool HasActiveOperation => _points.Count > 0;

    public override void Activate(IToolContext context)
    {
        base.Activate(context);
        _targets = new List<IEntity>(context.Selection.Items);
        ResetGesture();
    }

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (_targets.Count == 0)
        {
            // Nothing is selected to transform — hand back to the select tool so the user can pick.
            Context.ReturnToDefaultTool();
            return;
        }

        _points.Add(ResolvePoint(input));

        if (_points.Count >= RequiredPoints)
        {
            if (TryBuildTransform(_points, out Matrix2D transform))
                Commit(transform);
            ResetGesture();
        }

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = ResolvePoint(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        ResetGesture();
        Context.RequestRedraw();
    }

    /// <summary>Resolves a pointer event to a world point. Override to add ortho/angle constraints.</summary>
    protected virtual Point2D ResolvePoint(in ToolPointerInput input) => Snap(input);

    /// <summary>
    /// Builds the transform from the gesture's points (exactly <see cref="RequiredPoints"/> of
    /// them, the last possibly being the live cursor). Returns false for a degenerate gesture
    /// (e.g. a zero-length move or a collapsed mirror axis), in which case nothing is drawn or
    /// committed.
    /// </summary>
    protected abstract bool TryBuildTransform(IReadOnlyList<Point2D> points, out Matrix2D transform);

    /// <summary>Optional guide geometry (rubber line, mirror axis) drawn while the gesture runs.</summary>
    protected virtual void AppendGuides(List<OverlayItem> items, IReadOnlyList<Point2D> points)
    {
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_points.Count > 0 && _hasCursor)
        {
            IReadOnlyList<Point2D> points = EffectivePoints();
            if (TryBuildTransform(points, out Matrix2D transform) && transform != Matrix2D.Identity)
            {
                foreach (IEntity entity in _targets)
                {
                    IEntity preview = entity.Clone();
                    preview.Transform(transform);
                    items.Add(new OverlayItem(preview, ToolStyle.Preview));
                }
            }

            AppendGuides(items, points);
        }

        AddSnapMarker(items);
        return items;
    }

    /// <summary>The confirmed points plus the live cursor, padded out to <see cref="RequiredPoints"/>.</summary>
    private IReadOnlyList<Point2D> EffectivePoints()
    {
        if (_points.Count >= RequiredPoints)
            return _points;

        var points = new List<Point2D>(_points);
        while (points.Count < RequiredPoints)
            points.Add(_cursor);
        return points;
    }

    private void Commit(in Matrix2D transform)
    {
        if (transform == Matrix2D.Identity)
            return;

        if (Copy)
        {
            var adds = new List<IUndoableCommand>(_targets.Count);
            foreach (IEntity entity in _targets)
            {
                IEntity clone = entity.Clone();
                clone.Transform(transform);
                adds.Add(new AddEntityCommand(Context.Document, clone));
            }

            Context.Execute(new CompositeCommand(Name, adds));
        }
        else
        {
            Context.Execute(new TransformEntitiesCommand(Context.Document, _targets, transform));
        }
    }

    private void ResetGesture()
    {
        _points.Clear();
        _hasCursor = false;
        ClearSnap();
    }
}
