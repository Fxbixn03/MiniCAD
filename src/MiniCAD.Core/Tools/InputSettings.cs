namespace MiniCAD.Core.Tools;

/// <summary>
/// Angle-constraint options for point entry (Ortho and Polar tracking). When a constraint is
/// active the next segment point is locked to a ray from the previous point: Ortho restricts to
/// horizontal/vertical, Polar to multiples of <see cref="PolarAngleStepDegrees"/>. Object snapping
/// still wins when the cursor is actually over a snap target, so the two combine.
/// </summary>
public sealed class InputSettings
{
    /// <summary>A shared "no constraint" instance, used as the default when a host supplies none.</summary>
    public static readonly InputSettings None = new();

    /// <summary>Constrain to horizontal/vertical (90° steps) from the previous point.</summary>
    public bool OrthoEnabled { get; set; }

    /// <summary>Constrain to multiples of <see cref="PolarAngleStepDegrees"/> from the previous point.</summary>
    public bool PolarEnabled { get; set; }

    /// <summary>The polar tracking increment in degrees (e.g. 15, 30, 45).</summary>
    public double PolarAngleStepDegrees { get; set; } = 15.0;

    /// <summary>
    /// Resolves the effective angle step (in degrees) for a constrained point, or <c>null</c> for
    /// none. <paramref name="shift"/> temporarily toggles Ortho (held Shift turns it on, or off
    /// again if it is already on), matching the usual CAD behaviour.
    /// </summary>
    public double? AngleStepDegrees(bool shift)
    {
        if (OrthoEnabled ^ shift)
            return 90.0;
        if (PolarEnabled)
            return PolarAngleStepDegrees;
        return null;
    }
}
