using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Rendering;

/// <summary>
/// The drawing abstraction that decouples the domain from any concrete graphics backend.
/// Coordinates are expressed in <em>world</em> units; an implementation is responsible for
/// mapping them to device pixels (see the SkiaSharp implementation in MiniCAD.Renderer).
/// Entities render themselves by emitting these primitives, so a new entity type never has
/// to know about SkiaSharp and a new backend never has to know about entity types.
/// </summary>
public interface IRenderSurface
{
    /// <summary>
    /// Scale factor for scale-dependent fills (hatch) relative to the 1:100 base, set per Teilbild
    /// from its reference scale. Defaults to 1 for surfaces that don't vary it.
    /// </summary>
    double ModelScale => 1.0;

    void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke);

    void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke);

    void DrawCircle(Point2D center, double radius, in StrokeStyle stroke);

    /// <summary>
    /// Draws a circular arc. Angles are in radians, measured counter-clockwise from the
    /// positive X axis; <paramref name="sweepAngle"/> may be negative for clockwise arcs.
    /// </summary>
    void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke);

    /// <summary>
    /// Draws a single line of text. <paramref name="position"/> is the insertion point in world
    /// coordinates and <paramref name="height"/> the cap height in world units, so the text
    /// scales with zoom. <paramref name="rotation"/> is the baseline angle in radians, CCW from
    /// the world X axis (text stays upright/readable, never mirrored, despite the Y-flip). The
    /// alignment arguments anchor the run around <paramref name="position"/>. A null/empty
    /// <paramref name="fontFamily"/> uses the backend default; <paramref name="widthFactor"/>
    /// scales the glyphs horizontally (1.0 = normal).
    /// </summary>
    void DrawText(
        string text,
        Point2D position,
        double height,
        double rotation,
        TextHAlign horizontalAlignment,
        TextVAlign verticalAlignment,
        string? fontFamily,
        double widthFactor,
        in StrokeStyle stroke);
}
