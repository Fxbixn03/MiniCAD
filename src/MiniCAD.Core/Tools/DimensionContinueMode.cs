namespace MiniCAD.Core.Tools;

/// <summary>How the linear dimension tool continues after the first dimension is placed.</summary>
public enum DimensionContinueMode
{
    /// <summary>Each dimension is placed independently (three clicks).</summary>
    None,

    /// <summary>Continued (chain): the next dimension starts where the previous one ended.</summary>
    Chain,

    /// <summary>Baseline: every dimension starts from the common first point, staggered outward.</summary>
    Baseline,
}
