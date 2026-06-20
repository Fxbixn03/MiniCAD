using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tools;

/// <summary>Shared strokes for transient editor overlays (previews and highlights).</summary>
public static class ToolStyle
{
    /// <summary>Rubber-band preview while constructing geometry.</summary>
    public static readonly StrokeStyle Preview = new(new Color(80, 200, 255, 220), 1.5);

    /// <summary>Highlight drawn over selected entities.</summary>
    public static readonly StrokeStyle Selection = new(new Color(255, 170, 40, 255), 2.5);

    /// <summary>Marker drawn at the current snap target.</summary>
    public static readonly StrokeStyle SnapMarker = new(new Color(120, 255, 140, 255), 1.5);

    /// <summary>Vertex grip handle on the selected entity (endpoints / corners).</summary>
    public static readonly StrokeStyle VertexGrip = new(new Color(255, 200, 60, 255), 1.5);

    /// <summary>Edge / resize grip handle on the selected entity (segment midpoints, radius).</summary>
    public static readonly StrokeStyle EdgeGrip = new(new Color(90, 200, 255, 255), 1.5);

    /// <summary>Window box (left→right drag): selects only fully enclosed objects.</summary>
    public static readonly StrokeStyle WindowBox = new(new Color(80, 160, 255, 230), 1.2);

    /// <summary>Crossing box (right→left drag): also selects intersected objects.</summary>
    public static readonly StrokeStyle CrossingBox = new(new Color(110, 220, 130, 230), 1.2);
}
