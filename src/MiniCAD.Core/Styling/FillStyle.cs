namespace MiniCAD.Core.Styling;

/// <summary>
/// An area fill for a closed region: either a solid color or a linear gradient between two
/// colors at a given angle. Colors carry alpha, so fills can be semi-transparent. This is kept
/// separate from the stroke (outline) and from hatch patterns.
/// </summary>
public readonly struct FillStyle : IEquatable<FillStyle>
{
    public FillStyle(Color color, Color secondColor, bool isGradient, double angleDegrees)
    {
        Color = color;
        SecondColor = secondColor;
        IsGradient = isGradient;
        AngleDegrees = angleDegrees;
    }

    /// <summary>The solid color, or the gradient's start color.</summary>
    public Color Color { get; }

    /// <summary>The gradient's end color (ignored for a solid fill).</summary>
    public Color SecondColor { get; }

    /// <summary>True for a two-color linear gradient, false for a solid fill.</summary>
    public bool IsGradient { get; }

    /// <summary>Gradient direction in degrees, CCW from the world X axis.</summary>
    public double AngleDegrees { get; }

    public static FillStyle Solid(Color color) => new(color, color, false, 0.0);

    public static FillStyle Gradient(Color from, Color to, double angleDegrees) => new(from, to, true, angleDegrees);

    public bool Equals(FillStyle other)
        => Color == other.Color && SecondColor == other.SecondColor
        && IsGradient == other.IsGradient && AngleDegrees.Equals(other.AngleDegrees);

    public override bool Equals(object? obj) => obj is FillStyle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Color, SecondColor, IsGradient, AngleDegrees);

    public static bool operator ==(FillStyle a, FillStyle b) => a.Equals(b);

    public static bool operator !=(FillStyle a, FillStyle b) => !a.Equals(b);
}
