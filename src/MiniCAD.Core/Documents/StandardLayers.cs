using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>
/// The default layer set most professional planning offices work with. Exposed as plain
/// definitions (name + stroke) so the App can offer "insert standard layers" without baking
/// the catalogue into the UI. Colours and line weights follow common CAD conventions.
/// </summary>
public static class StandardLayers
{
    /// <summary>A single standard layer definition.</summary>
    public readonly record struct Definition(string Name, Color Color, double Width)
    {
        public StrokeStyle Stroke => new(Color, Width);
    }

    /// <summary>The catalogue, in a sensible drawing order.</summary>
    public static IReadOnlyList<Definition> All { get; } = new[]
    {
        new Definition("Achsen", new Color(120, 120, 140), 0.5),
        new Definition("Wände tragend", new Color(235, 235, 235), 2.5),
        new Definition("Wände nichttragend", new Color(180, 180, 180), 1.5),
        new Definition("Stützen", new Color(235, 235, 235), 2.5),
        new Definition("Türen", new Color(120, 190, 235), 1.0),
        new Definition("Fenster", new Color(120, 210, 200), 1.0),
        new Definition("Treppen", new Color(200, 160, 120), 1.0),
        new Definition("Möblierung", new Color(150, 200, 150), 0.7),
        new Definition("Sanitär", new Color(90, 170, 230), 0.7),
        new Definition("Elektro", new Color(230, 200, 90), 0.7),
        new Definition("Bemaßung", new Color(230, 150, 150), 0.35),
        new Definition("Beschriftung", new Color(230, 230, 230), 0.35),
        new Definition("Schraffur", new Color(160, 160, 160), 0.35),
    };
}
