using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Snapping;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Convenience base class with no-op defaults so a concrete tool only overrides the handful
/// of members it actually needs. By default <see cref="ToolKey.Escape"/> cancels the tool.
/// Also provides shared snapping helpers for drawing tools.
/// </summary>
public abstract class ToolBase : ITool
{
    protected IToolContext Context { get; private set; } = null!;

    /// <summary>The most recent snap result, used to draw the snap marker.</summary>
    protected SnapResult CurrentSnap { get; private set; }

    public abstract string Name { get; }

    public virtual void Activate(IToolContext context) => Context = context;

    public virtual void Deactivate() => Cancel();

    public virtual void PointerDown(in ToolPointerInput input)
    {
    }

    public virtual void PointerMove(in ToolPointerInput input)
    {
    }

    public virtual void PointerUp(in ToolPointerInput input)
    {
    }

    public virtual void KeyDown(ToolKey key)
    {
        if (key != ToolKey.Escape)
            return;

        if (HasActiveOperation)
            Cancel();          // abort the in-progress construction, keep the tool active
        else
            OnEscapeIdle();    // nothing going on: fall back to the default (select) tool
    }

    /// <summary>True while the tool is mid-construction (e.g. a line has its first point placed).</summary>
    protected virtual bool HasActiveOperation => false;

    /// <summary>Reaction to Escape when there is nothing to cancel; returns to the select tool.</summary>
    protected virtual void OnEscapeIdle() => Context.ReturnToDefaultTool();

    public virtual void Cancel()
    {
    }

    /// <summary>
    /// Stamps the context's default stroke override (set by the Assistant) onto a freshly
    /// created entity, then returns it. Drawing tools call this before committing.
    /// </summary>
    protected TEntity ApplyDefaultStyle<TEntity>(TEntity entity) where TEntity : Entities.IEntity
    {
        if (Context.DefaultStrokeOverride is { } stroke)
            entity.StrokeOverride = stroke;
        if (Context.ConstructionMode)
            entity.IsConstruction = true;
        return entity;
    }

    public virtual IReadOnlyList<OverlayItem> GetOverlay() => Array.Empty<OverlayItem>();

    /// <summary>Snaps the input point, remembering the result so <see cref="AddSnapMarker"/> can show it.</summary>
    protected Point2D Snap(in ToolPointerInput input)
    {
        CurrentSnap = Context.Snap(input.World);
        return CurrentSnap.Point;
    }

    /// <summary>
    /// Resolves the next point of a segment-drawing tool. When an angle constraint is active —
    /// Ortho/Polar from <see cref="InputSettings"/>, or temporarily via held Shift — the point is
    /// locked to the nearest matching ray from <paramref name="origin"/>; an object snap target
    /// still wins when the cursor is over one, so the two combine. Otherwise normal snapping
    /// applies.
    /// </summary>
    protected Point2D ResolveSegmentPoint(Point2D origin, in ToolPointerInput input)
    {
        if (Context.InputSettings.AngleStepDegrees(input.HasShift) is not { } stepDegrees)
            return Snap(input);

        // Object snapping still takes precedence when the cursor is actually over a snap target.
        SnapResult snap = Context.Snap(input.World);
        if (snap.IsSnapped)
        {
            CurrentSnap = snap;
            return snap.Point;
        }

        ClearSnap();
        return GeometryMath.SnapToAngleStep(origin, input.World, GeometryMath.DegreesToRadians(stepDegrees));
    }

    /// <summary>Forgets the current snap so its marker disappears (e.g. after committing).</summary>
    protected void ClearSnap() => CurrentSnap = default;

    /// <summary>Appends a small square marker at the current snap target, if any.</summary>
    protected void AddSnapMarker(List<OverlayItem> items)
    {
        if (!CurrentSnap.IsSnapped)
            return;

        double half = Context.PickTolerance * 1.6;
        Point2D p = CurrentSnap.Point;
        var marker = PolylineEntity.Rectangle(
            new Point2D(p.X - half, p.Y - half),
            new Point2D(p.X + half, p.Y + half));
        items.Add(new OverlayItem(marker, ToolStyle.SnapMarker));
    }
}
