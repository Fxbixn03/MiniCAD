namespace MiniCAD.App.ViewModels;

/// <summary>Shared option lists for the styling combo boxes.</summary>
public static class StyleOptions
{
    /// <summary>Standard ISO plot line weights in mm; 0 means "use the pixel width" (Anzeige).</summary>
    public static double[] LineWeights => new[] { 0.0, 0.13, 0.18, 0.25, 0.35, 0.50, 0.70, 1.00, 1.40, 2.00 };
}
