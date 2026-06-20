namespace MiniCAD.Core.Styling;

/// <summary>
/// Describes how an outline is stroked. <see cref="Width"/> is interpreted in device
/// pixels by the renderer, so line weight stays constant regardless of zoom level.
/// <see cref="LineType"/> selects a dash pattern (also rendered in device pixels).
/// </summary>
public readonly struct StrokeStyle : IEquatable<StrokeStyle>
{
    public static readonly StrokeStyle Default = new(Color.White, 1.0);

    public StrokeStyle(Color color, double width = 1.0, LineType lineType = LineType.Solid, double lineWeightMm = 0.0)
    {
        Color = color;
        Width = width <= 0.0 ? 1.0 : width;
        LineType = lineType;
        LineWeightMm = lineWeightMm < 0.0 ? 0.0 : lineWeightMm;
    }

    public Color Color { get; }

    /// <summary>Fallback stroke width in device pixels (used when <see cref="LineWeightMm"/> is 0).</summary>
    public double Width { get; }

    /// <summary>The dash pattern applied to the stroke.</summary>
    public LineType LineType { get; }

    /// <summary>
    /// Real plot line weight in millimetres. When &gt; 0 it drives the rendered/printed width
    /// (converted mm→px by the backend); 0 means "use <see cref="Width"/> device pixels".
    /// </summary>
    public double LineWeightMm { get; }

    public StrokeStyle WithColor(Color color) => new(color, Width, LineType, LineWeightMm);

    public StrokeStyle WithWidth(double width) => new(Color, width, LineType, LineWeightMm);

    public StrokeStyle WithLineType(LineType lineType) => new(Color, Width, lineType, LineWeightMm);

    public StrokeStyle WithLineWeight(double lineWeightMm) => new(Color, Width, LineType, lineWeightMm);

    public bool Equals(StrokeStyle other)
        => Color == other.Color && Width.Equals(other.Width)
        && LineType == other.LineType && LineWeightMm.Equals(other.LineWeightMm);

    public override bool Equals(object? obj) => obj is StrokeStyle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Color, Width, LineType, LineWeightMm);

    public static bool operator ==(StrokeStyle a, StrokeStyle b) => a.Equals(b);

    public static bool operator !=(StrokeStyle a, StrokeStyle b) => !a.Equals(b);
}
