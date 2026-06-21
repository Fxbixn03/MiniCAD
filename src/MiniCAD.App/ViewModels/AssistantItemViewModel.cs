using System.Globalization;
using Avalonia.Media;
using CoreStroke = MiniCAD.Core.Styling.StrokeStyle;

namespace MiniCAD.App.ViewModels;

/// <summary>Which drawing tool an Assistant template re-activates.</summary>
public enum AssistantToolKind
{
    Line,
    Rectangle,
    Circle,
    Polyline,
    Wall,
    Opening,
    Column,
    Slab,
    Beam,
}

/// <summary>
/// A single reusable Assistant template ("Vorlage"): a named, predefined element the user can
/// double-click to start drawing again with the same tool, line style — and, for architectural
/// elements, the same thickness/height/elevation/cross-section and layer.
/// </summary>
public sealed class AssistantItemViewModel
{
    public AssistantItemViewModel(string name, AssistantToolKind toolKind, CoreStroke? stroke, bool isBuiltIn,
        string? layerName = null, bool round = false, double thickness = 0, double height = 0,
        double baseElevation = 0, double width = 0, double depth = 0)
    {
        Name = name;
        ToolKind = toolKind;
        Stroke = stroke;
        IsBuiltIn = isBuiltIn;
        LayerName = layerName;
        Round = round;
        Thickness = thickness;
        Height = height;
        BaseElevation = baseElevation;
        Width = width;
        Depth = depth;
    }

    public string Name { get; }

    public AssistantToolKind ToolKind { get; }

    /// <summary>The stroke applied to elements drawn from this template (null = layer default).</summary>
    public CoreStroke? Stroke { get; }

    public bool IsBuiltIn { get; }

    /// <summary>Layer activated when the template is used (created on demand); null = keep current.</summary>
    public string? LayerName { get; }

    // Architectural parameters (0 / false when not applicable).
    public bool Round { get; }
    public double Thickness { get; }
    public double Height { get; }
    public double BaseElevation { get; }
    public double Width { get; }
    public double Depth { get; }

    public bool IsArchitectural => ToolKind is AssistantToolKind.Wall or AssistantToolKind.Opening
        or AssistantToolKind.Column or AssistantToolKind.Slab or AssistantToolKind.Beam;

    public string ToolLabel => ToolKind switch
    {
        AssistantToolKind.Line => "Linie",
        AssistantToolKind.Rectangle => "Rechteck",
        AssistantToolKind.Circle => "Kreis",
        AssistantToolKind.Polyline => "Polylinie",
        AssistantToolKind.Wall => "Wand",
        AssistantToolKind.Opening => "Aussparung",
        AssistantToolKind.Column => "Stütze",
        AssistantToolKind.Slab => "Decke",
        AssistantToolKind.Beam => "Unterzug",
        _ => ToolKind.ToString(),
    };

    /// <summary>A short parameter summary shown in the list row.</summary>
    public string Detail
    {
        get
        {
            string layer = LayerName is { Length: > 0 } ? $" · {LayerName}" : string.Empty;
            return ToolKind switch
            {
                AssistantToolKind.Wall => $"d {N(Thickness)} · H {N(Height)}{layer}",
                AssistantToolKind.Opening => $"H {N(Height)} · OK {N(BaseElevation)}{layer}",
                AssistantToolKind.Column => (Round ? $"⌀{N(Width)}" : $"{N(Width)}×{N(Depth)}") + $" · H {N(Height)}{layer}",
                AssistantToolKind.Slab => $"d {N(Thickness)}{layer}",
                AssistantToolKind.Beam => $"{N(Width)}×{N(Height)}{layer}",
                _ => Stroke is { } s ? $"{s.Width:0.#} px" : string.Empty,
            };
        }
    }

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A swatch showing the template's line colour for the list row.</summary>
    public IBrush PreviewBrush => Stroke is { } stroke
        ? new SolidColorBrush(Color.FromArgb(stroke.Color.A, stroke.Color.R, stroke.Color.G, stroke.Color.B))
        : Brushes.Gray;

    public double PreviewWidth => Stroke?.Width ?? 1.0;
}
