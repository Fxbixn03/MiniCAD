using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Breaks an object (#187). First click picks the target; then, in single-point mode, one more
/// click splits it into two independent entities at the snapped point, while in two-point mode two
/// clicks remove the piece between them. Honours object snapping for precise break points.
/// </summary>
public sealed class BreakTool : ToolBase
{
    private IEntity? _target;
    private Point2D? _firstPoint;

    /// <summary>When true the tool removes the segment between two picked points (Break);
    /// otherwise it splits at a single point (Break-at-Point).</summary>
    public bool TwoPoint { get; set; }

    public override string Name => TwoPoint ? "Bruch" : "Trennen";

    protected override bool HasActiveOperation => _target is not null;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (_target is null)
        {
            _target = PickTopmost(input.World);
            Context.RequestRedraw();
            return;
        }

        Point2D point = Snap(input);

        if (!TwoPoint)
        {
            Apply(BreakGeometry.SplitAt(_target, point));
            return;
        }

        if (_firstPoint is null)
        {
            _firstPoint = point;
            Context.RequestRedraw();
            return;
        }

        Apply(BreakGeometry.BreakBetween(_target, _firstPoint.Value, point));
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        Snap(input);
        Context.RequestRedraw();
    }

    private void Apply(IReadOnlyList<IEntity> result)
    {
        IEntity target = _target!;

        // A single result that is the target itself means nothing changed.
        bool changed = !(result.Count == 1 && ReferenceEquals(result[0], target));
        if (changed)
        {
            var commands = new List<IUndoableCommand> { new RemoveEntityCommand(Context.Document, target) };
            foreach (IEntity part in result)
                commands.Add(new AddEntityCommand(Context.Document, part));
            Context.Execute(new CompositeCommand(Name, commands));
        }

        Cancel();
    }

    public override void Cancel()
    {
        _target = null;
        _firstPoint = null;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_target is { } target)
            items.Add(new OverlayItem(target, ToolStyle.Selection));
        AddSnapMarker(items);
        return items;
    }

    private IEntity? PickTopmost(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (BreakGeometry.CanBreak(entity) && Context.Document.IsEntityEditable(entity)
                && entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }
}
