using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Copies the current selection into a rectangular grid (rows/columns/spacing) or a polar
/// pattern (count/angle about a centre). Parameters come from the inline options bar. In
/// rectangular mode a left click (or Enter) confirms; in polar mode the click sets the centre.
/// A live preview is shown and all copies are added as one undoable step.
/// </summary>
public sealed class ArrayTool : ToolBase
{
    private List<IEntity> _targets = new();
    private Point2D _cursor;
    private bool _hasCursor;

    // Set from the App's options bar.
    public int Rows { get; set; } = 2;
    public int Columns { get; set; } = 3;
    public double SpacingX { get; set; } = 10;
    public double SpacingY { get; set; } = 10;
    public bool Polar { get; set; }
    public int Count { get; set; } = 6;
    public double TotalAngle { get; set; } = Math.PI * 2.0;

    public override string Name => "Array";

    protected override bool HasActiveOperation => _targets.Count > 0;

    public override void Activate(IToolContext context)
    {
        base.Activate(context);
        _targets = new List<IEntity>(context.Selection.Items);
    }

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (_targets.Count == 0)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        Commit(Polar ? PolarTransforms(Snap(input)) : RectangularTransforms());
        ClearSnap();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void KeyDown(ToolKey key)
    {
        // Rectangular arrays don't need a point, so Enter confirms them on the spot.
        if (key == ToolKey.Enter && !Polar && _targets.Count > 0)
        {
            Commit(RectangularTransforms());
            Context.RequestRedraw();
            return;
        }

        base.KeyDown(key);
    }

    private IReadOnlyList<Matrix2D> RectangularTransforms()
        => ArrayGenerator.Rectangular(Rows, Columns, SpacingX, SpacingY);

    private IReadOnlyList<Matrix2D> PolarTransforms(Point2D center)
        => ArrayGenerator.Polar(center, Count, TotalAngle);

    private void Commit(IReadOnlyList<Matrix2D> transforms)
    {
        if (transforms.Count == 0)
            return;

        var commands = new List<IUndoableCommand>(transforms.Count * _targets.Count);
        foreach (Matrix2D transform in transforms)
        {
            foreach (IEntity target in _targets)
            {
                IEntity clone = target.Clone();
                clone.Transform(transform);
                commands.Add(new AddEntityCommand(Context.Document, clone));
            }
        }

        Context.Execute(new CompositeCommand(Name, commands));
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_targets.Count == 0)
            return items;

        IReadOnlyList<Matrix2D> transforms = Polar
            ? (_hasCursor ? PolarTransforms(_cursor) : Array.Empty<Matrix2D>())
            : RectangularTransforms();

        foreach (Matrix2D transform in transforms)
        {
            foreach (IEntity target in _targets)
            {
                IEntity preview = target.Clone();
                preview.Transform(transform);
                items.Add(new OverlayItem(preview, ToolStyle.Preview));
            }
        }

        AddSnapMarker(items);
        return items;
    }
}
