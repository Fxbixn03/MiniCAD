using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Rounds (fillet) or bevels (chamfer) the corner between two lines: click the first line on the
/// side to keep, then the second. <see cref="Distance"/> is the fillet radius or the chamfer
/// setback; 0 makes a sharp corner. Each join is one undoable step and the tool stays active.
/// </summary>
public sealed class FilletTool : ToolBase
{
    private LineEntity? _first;
    private Point2D _firstPick;
    private Point2D _cursor;
    private bool _hasCursor;

    /// <summary>Fillet radius, or chamfer setback when <see cref="Chamfer"/> is true. 0 = sharp corner.</summary>
    public double Distance { get; set; }

    /// <summary>When true the corner is bevelled with a straight chamfer instead of a tangent arc.</summary>
    public bool Chamfer { get; set; }

    public override string Name => Chamfer ? "Fase" : "Abrundung";

    protected override bool HasActiveOperation => _first is not null;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (PickLine(input.World) is not { } line)
            return;

        if (_first is null)
        {
            _first = line;
            _firstPick = input.World;
        }
        else if (!ReferenceEquals(line, _first))
        {
            Apply(_first, _firstPick, line, input.World);
            _first = null;
        }

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
        _first = null;
        _hasCursor = false;
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_first is { } first)
        {
            items.Add(new OverlayItem(first, ToolStyle.Selection));
            if (_hasCursor && PickLine(_cursor) is { } second && !ReferenceEquals(second, first)
                && FilletGeometry.Compute(first, _firstPick, second, _cursor, Distance, Chamfer) is { } preview)
            {
                items.Add(new OverlayItem(new LineEntity(preview.Keep1, preview.Tangent1), ToolStyle.Preview));
                items.Add(new OverlayItem(new LineEntity(preview.Keep2, preview.Tangent2), ToolStyle.Preview));
                if (preview.Connector is { } connector)
                    items.Add(new OverlayItem(connector, ToolStyle.Preview));
            }
        }

        return items;
    }

    private void Apply(LineEntity first, Point2D firstPick, LineEntity second, Point2D secondPick)
    {
        if (FilletGeometry.Compute(first, firstPick, second, secondPick, Distance, Chamfer) is not { } result)
            return;

        var commands = new List<IUndoableCommand>(3)
        {
            EditLine(first, result.Keep1, result.Tangent1),
            EditLine(second, result.Keep2, result.Tangent2),
        };
        if (result.Connector is { } connector)
            commands.Add(new AddEntityCommand(Context.Document, connector));

        Context.Execute(new CompositeCommand(Name, commands));
    }

    private EditEntityCommand EditLine(LineEntity line, Point2D keep, Point2D tangent)
    {
        object before = line.CaptureState();
        line.Start = keep;
        line.End = tangent;
        object after = line.CaptureState();
        line.RestoreState(before); // command re-applies on Execute
        return new EditEntityCommand(Context.Document, line, before, after);
    }

    private LineEntity? PickLine(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            if (entities[i] is LineEntity line && Context.Document.IsEntityEditable(line) && line.HitTest(world, tolerance))
                return line;
        }

        return null;
    }
}
