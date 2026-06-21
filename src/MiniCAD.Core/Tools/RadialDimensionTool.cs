using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places a radius or diameter dimension. The first click picks a circle/arc (its centre and
/// radius are adopted and the arrow lands where you clicked); the second click sets the text
/// position. <see cref="Diameter"/> switches between R and ⌀.
/// </summary>
public sealed class RadialDimensionTool : ToolBase
{
    private Point2D _center;
    private Point2D _edge;
    private bool _picked;
    private Point2D _cursor;
    private bool _hasCursor;

    /// <summary>When true the tool measures the diameter (⌀) instead of the radius (R).</summary>
    public bool Diameter { get; set; }

    public override string Name => "Radialmaß";

    protected override bool HasActiveOperation => _picked;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            if (HasActiveOperation)
                Cancel();
            else
                Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (!_picked)
        {
            if (TryPickCircle(input.World, out _center, out double radius))
            {
                Vector2D dir = (input.World - _center).Normalized();
                if (dir == Vector2D.Zero)
                    dir = Vector2D.UnitX;
                _edge = _center + dir * radius;
                _picked = true;
                Context.RequestRedraw();
            }

            return;
        }

        var dim = new RadialDimensionEntity(_center, _edge, input.World, Diameter);
        CadDocument.ApplyDimStyle(dim, Context.Document.ActiveDimStyle);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(dim)));
        Cancel();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = input.World;
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _picked = false;
        ClearSnap();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        if (_picked && _hasCursor)
        {
            var preview = new RadialDimensionEntity(_center, _edge, _cursor, Diameter);
            CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
            return new[] { new OverlayItem(preview, ToolStyle.Preview) };
        }

        return Array.Empty<OverlayItem>();
    }

    private bool TryPickCircle(Point2D world, out Point2D center, out double radius)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (!Context.Document.IsEntityEditable(entity) || !entity.HitTest(world, tolerance))
                continue;

            switch (entity)
            {
                case CircleEntity circle:
                    center = circle.Center;
                    radius = circle.Radius;
                    return true;
                case ArcEntity arc:
                    center = arc.Center;
                    radius = arc.Radius;
                    return true;
            }
        }

        center = default;
        radius = 0;
        return false;
    }
}
