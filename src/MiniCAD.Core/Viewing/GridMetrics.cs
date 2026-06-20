namespace MiniCAD.Core.Viewing;

/// <summary>
/// Shared logic for the reference grid spacing, so the rendered grid and grid snapping
/// always agree on where the grid lines are.
/// </summary>
public static class GridMetrics
{
    /// <summary>Target on-screen spacing (device pixels) between adjacent grid lines.</summary>
    public const double TargetPixelSpacing = 80.0;

    /// <summary>The world-space grid spacing for a given zoom, rounded to a 1/2/5 × 10ⁿ value.</summary>
    public static double StepFor(double scale)
    {
        double safeScale = scale <= 0 ? 1.0 : scale;
        return NiceStep(TargetPixelSpacing / safeScale);
    }

    /// <summary>Rounds a raw spacing up to the nearest "nice" 1/2/5 × 10ⁿ value.</summary>
    public static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw))
            return 1.0;

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double normalized = raw / magnitude;
        double factor = normalized < 1.5 ? 1.0 : normalized < 3.0 ? 2.0 : normalized < 7.0 ? 5.0 : 10.0;
        return factor * magnitude;
    }
}
