using System.Globalization;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Base class for all dimensions. It carries the (style-driven) appearance — text height,
/// arrow size, extension-line offset/overshoot and rounding — plus an optional manual text
/// override, and provides the shared render helpers (arrowheads, measurement text, readable
/// text angle). The measured value and the actual geometry are supplied by the subclasses.
/// </summary>
public abstract class DimensionEntity : Entity
{
    /// <summary>Half-angle of the arrowhead barbs, in radians.</summary>
    protected const double BarbAngle = 0.32;

    protected DimensionEntity()
    {
    }

    protected DimensionEntity(DimensionEntity source) : base(source)
    {
        DimStyleId = source.DimStyleId;
        TextHeight = source.TextHeight;
        ArrowSize = source.ArrowSize;
        ExtensionOffset = source.ExtensionOffset;
        ExtensionOvershoot = source.ExtensionOvershoot;
        DecimalPlaces = source.DecimalPlaces;
        TextOverride = source.TextOverride;
    }

    /// <summary>Id of the assigned <c>DimStyle</c> (<see cref="Guid.Empty"/> = none).</summary>
    public Guid DimStyleId { get; set; }

    public double TextHeight { get; set; } = 12.0;

    public double ArrowSize { get; set; } = 10.0;

    public double ExtensionOffset { get; set; } = 3.0;

    public double ExtensionOvershoot { get; set; } = 5.0;

    public int DecimalPlaces { get; set; }

    /// <summary>Manual measurement text; when null the value is formatted automatically.</summary>
    public string? TextOverride { get; set; }

    /// <summary>The raw measured quantity (length, angle in degrees, radius, …).</summary>
    public abstract double MeasuredValue { get; }

    /// <summary>Prefix for the automatic text (e.g. "R", "⌀").</summary>
    protected virtual string Prefix => string.Empty;

    /// <summary>Suffix for the automatic text (e.g. "°").</summary>
    protected virtual string Suffix => string.Empty;

    /// <summary>The text shown on the dimension: the override, or the formatted measured value.</summary>
    public virtual string DisplayText => TextOverride ?? Prefix + FormatValue(MeasuredValue) + Suffix;

    protected string FormatValue(double value)
        => value.ToString("F" + DecimalPlaces.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Draws an open arrowhead whose tip is at <paramref name="tip"/> pointing along <paramref name="towards"/>.</summary>
    protected void DrawArrow(IRenderSurface surface, Point2D tip, Vector2D towards, in StrokeStyle stroke)
    {
        if (ArrowSize <= 0.0)
            return;

        Vector2D dir = towards.Normalized();
        if (dir == Vector2D.Zero)
            return;

        Vector2D back = -dir * ArrowSize;
        Point2D b1 = tip + Rotate(back, BarbAngle);
        Point2D b2 = tip + Rotate(back, -BarbAngle);
        surface.DrawPolyline(new[] { b1, tip, b2 }, closed: false, stroke);
    }

    /// <summary>Draws the measurement text centred at <paramref name="position"/>, rotated to read upright.</summary>
    protected void DrawMeasurementText(IRenderSurface surface, Point2D position, double angle, in StrokeStyle stroke)
        => surface.DrawText(DisplayText, position, TextHeight, ReadableAngle(angle),
            TextHAlign.Center, TextVAlign.Bottom, null, 1.0, stroke);

    /// <summary>Keeps a baseline angle within (-90°, 90°] so the text never reads upside down.</summary>
    protected static double ReadableAngle(double radians)
    {
        double a = Math.Atan2(Math.Sin(radians), Math.Cos(radians)); // normalize to (-π, π]
        if (a > Math.PI / 2)
            a -= Math.PI;
        else if (a <= -Math.PI / 2)
            a += Math.PI;
        return a;
    }

    protected static Vector2D Rotate(Vector2D v, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new Vector2D(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
