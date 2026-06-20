using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Trims or extends objects at the edges formed by the other objects. Click the part of an
/// object to cut away (the span between the two nearest crossings disappears); hold Shift and
/// click near an open end to extend it to the next object instead. Each edit is one undoable
/// step. The tool stays active so several cuts can be made in a row.
/// </summary>
public sealed class TrimExtendTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Stutzen/Dehnen";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        IEntity? target = PickTopmost(input.World);
        if (target is null)
            return;

        if (input.HasShift)
            Extend(target, input.World);
        else
            Trim(target, input.World);

        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = input.World;
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_hasCursor && PickTopmost(_cursor) is { } hover)
            items.Add(new OverlayItem(hover, ToolStyle.Selection));
        return items;
    }

    private void Trim(IEntity target, Point2D pick)
    {
        var cutPoints = new List<Point2D>();
        foreach (IEntity other in Cutters(target))
            cutPoints.AddRange(EntityIntersection.Between(target, other));

        if (TrimGeometry.Trim(target, pick, cutPoints) is not { } pieces)
            return;

        var commands = new List<IUndoableCommand>(pieces.Count + 1)
        {
            new RemoveEntityCommand(Context.Document, target),
        };
        foreach (IEntity piece in pieces)
            commands.Add(new AddEntityCommand(Context.Document, piece));

        Context.Execute(new CompositeCommand("Stutzen", commands));
    }

    private void Extend(IEntity target, Point2D pick)
    {
        if (TrimGeometry.Extend(target, pick, Cutters(target)) is not { } extended)
            return;

        Context.Execute(new CompositeCommand("Dehnen", new IUndoableCommand[]
        {
            new RemoveEntityCommand(Context.Document, target),
            new AddEntityCommand(Context.Document, extended),
        }));
    }

    private List<IEntity> Cutters(IEntity target)
    {
        var cutters = new List<IEntity>();
        foreach (IEntity entity in Context.Document.Entities)
        {
            if (!ReferenceEquals(entity, target) && Context.Document.IsEntityEditable(entity))
                cutters.Add(entity);
        }

        return cutters;
    }

    private IEntity? PickTopmost(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (Context.Document.IsEntityEditable(entity) && entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }
}
