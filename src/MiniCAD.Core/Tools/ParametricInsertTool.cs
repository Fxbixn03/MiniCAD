using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Inserts parametric catalog symbols (Smartsymbole): each left click drops a
/// <see cref="ParametricSymbolEntity"/> of the current <see cref="SymbolKey"/> with the chosen
/// <see cref="Parameters"/> at the snapped cursor. A live preview follows the cursor.
/// </summary>
public sealed class ParametricInsertTool : ToolBase
{
    private Point2D _cursor;
    private bool _hasCursor;

    public string? SymbolKey { get; set; }

    public Dictionary<string, double> Parameters { get; set; } = new();

    public override string Name => "Smartsymbol";

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left || SymbolKey is null)
            return;

        var symbol = new ParametricSymbolEntity(SymbolKey, Snap(input), Parameters);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(symbol)));
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
        if (_hasCursor && SymbolKey is { } key)
            items.Add(new OverlayItem(new ParametricSymbolEntity(key, _cursor, Parameters), ToolStyle.Preview));

        AddSnapMarker(items);
        return items;
    }
}
