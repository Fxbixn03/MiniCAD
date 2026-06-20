using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Creates a parallel copy of an object at the distance and side picked with the cursor: first
/// click selects the source object, then move to the desired side and click (or type a point) to
/// place the offset. Works on lines, circles, arcs and polylines (incl. closed ones). Each offset
/// is one undoable step; the source stays selected so further offsets can be placed.
/// </summary>
public sealed class OffsetTool : ToolBase
{
    private IEntity? _source;
    private Point2D _cursor;
    private bool _hasCursor;

    public override string Name => "Parallele";

    protected override bool HasActiveOperation => _source is not null;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (_source is null)
        {
            _source = PickTopmost(input.World);
            Context.RequestRedraw();
            return;
        }

        Point2D through = Snap(input);
        if (OffsetGeometry.Offset(_source, through) is { } offset)
            Context.Execute(new AddEntityCommand(Context.Document, offset));

        ClearSnap();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel()
    {
        _source = null;
        _hasCursor = false;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_source is { } source)
        {
            items.Add(new OverlayItem(source, ToolStyle.Selection));
            if (_hasCursor && OffsetGeometry.Offset(source, _cursor) is { } preview)
                items.Add(new OverlayItem(preview, ToolStyle.Preview));
        }

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
            if (Context.Document.IsEntityEditable(entity) && entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }
}
