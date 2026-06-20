namespace MiniCAD.Core.Styling;

/// <summary>
/// Describes how an outline is stroked. <see cref="Width"/> is interpreted in device
/// pixels by the renderer, so line weight stays constant regardless of zoom level.
/// <see cref="LineType"/> selects a dash pattern (also rendered in device pixels).
/// </summary>
public readonly struct StrokeStyle : IEquatable<StrokeStyle>
{
    public static readonly StrokeStyle Default = new(Color.White, 1.0);

    public StrokeStyle(Color color, double width = 1.0, LineType lineType = LineType.Solid)
    {
        Color = color;
        Width = width <= 0.0 ? 1.0 : width;
        LineType = lineType;
    }

    public Color Color { get; }

    /// <summary>Stroke width in device pixels (always positive).</summary>
    public double Width { get; }

    /// <summary>The dash pattern applied to the stroke.</summary>
    public LineType LineType { get; }

    public StrokeStyle WithColor(Color color) => new(color, Width, LineType);

    public StrokeStyle WithWidth(double width) => new(Color, width, LineType);

    public StrokeStyle WithLineType(LineType lineType) => new(Color, Width, lineType);

    public bool Equals(StrokeStyle other) => Color == other.Color && Width.Equals(other.Width) && LineType == other.LineType;

    public override bool Equals(object? obj) => obj is StrokeStyle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Color, Width, LineType);

    public static bool operator ==(StrokeStyle a, StrokeStyle b) => a.Equals(b);

    public static bool operator !=(StrokeStyle a, StrokeStyle b) => !a.Equals(b);
}
