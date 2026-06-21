using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Inserts instances of <see cref="Definition"/>: each left click drops a
/// <see cref="BlockReferenceEntity"/> at the snapped cursor with the current scale/rotation.
/// A live preview follows the cursor. Stays active for repeated placement.
/// </summary>
public sealed class BlockInsertTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    /// <summary>The block definition placed by the next click.</summary>
    public BlockDefinition? Definition { get; set; }

    public double Scale { get; set; } = 1.0;

    public double Rotation { get; set; }

    public override string Name => "Block einfügen";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left || Definition is null)
            return;

        var reference = new BlockReferenceEntity(Definition, Snap(input), Scale, Rotation);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(reference)));
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);
        if (_hasCursor && Definition is { } definition)
            items.Add(new OverlayItem(new BlockReferenceEntity(definition, _cursor, Scale, Rotation), ToolStyle.Preview));

        AddSnapMarker(items);
        return items;
    }
}
