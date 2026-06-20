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
}

/// <summary>
/// A single reusable Assistant template ("Vorlage"): a named, predefined element the user can
/// double-click to start drawing again with the same tool and line style.
/// </summary>
public sealed class AssistantItemViewModel
{
    public AssistantItemViewModel(string name, AssistantToolKind toolKind, CoreStroke? stroke, bool isBuiltIn)
    {
        Name = name;
        ToolKind = toolKind;
        Stroke = stroke;
        IsBuiltIn = isBuiltIn;
    }

    public string Name { get; }

    public AssistantToolKind ToolKind { get; }

    /// <summary>The stroke applied to elements drawn from this template (null = layer default).</summary>
    public CoreStroke? Stroke { get; }

    public bool IsBuiltIn { get; }

    public string ToolLabel => ToolKind switch
    {
        AssistantToolKind.Line => "Linie",
        AssistantToolKind.Rectangle => "Rechteck",
        AssistantToolKind.Circle => "Kreis",
        AssistantToolKind.Polyline => "Polylinie",
        _ => ToolKind.ToString(),
    };

    /// <summary>A swatch showing the template's line colour for the list row.</summary>
    public IBrush PreviewBrush => Stroke is { } stroke
        ? new SolidColorBrush(Color.FromArgb(stroke.Color.A, stroke.Color.R, stroke.Color.G, stroke.Color.B))
        : Brushes.Gray;

    public double PreviewWidth => Stroke?.Width ?? 1.0;
}
