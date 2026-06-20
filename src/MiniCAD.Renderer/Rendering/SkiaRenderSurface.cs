using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;
using SkiaSharp;

namespace MiniCAD.Renderer.Rendering;

/// <summary>
/// SkiaSharp implementation of <see cref="IRenderSurface"/>. World coordinates are
/// transformed to device pixels in managed code so that stroke widths stay constant in
/// pixels regardless of zoom (the usual CAD expectation). This is the only place in the
/// solution that translates the abstract drawing primitives into SkiaSharp calls.
/// </summary>
internal sealed class SkiaRenderSurface : IRenderSurface, IDisposable
{
    private readonly SKCanvas _canvas;
    private readonly Matrix2D _worldToScreen;
    private readonly SKPaint _paint;
    private readonly SKPaint _textPaint;
    private readonly SKFont _font;

    public SkiaRenderSurface(SKCanvas canvas, in Matrix2D worldToScreen)
    {
        _canvas = canvas;
        _worldToScreen = worldToScreen;
        _paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        // Text is filled (not stroked) so glyphs read cleanly at any size.
        _textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _font = new SKFont(SKTypeface.Default);
    }

    /// <summary>Scale-dependent fill factor (set per Teilbild from its reference scale).</summary>
    public double ModelScale { get; set; } = 1.0;

    public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke)
    {
        ApplyStroke(stroke);
        _canvas.DrawLine(ToDevice(a), ToDevice(b), _paint);
    }

    public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke)
    {
        if (points.Count < 2)
            return;

        ApplyStroke(stroke);
        using var path = new SKPath();
        path.MoveTo(ToDevice(points[0]));
        for (int i = 1; i < points.Count; i++)
            path.LineTo(ToDevice(points[i]));
        if (closed)
            path.Close();

        _canvas.DrawPath(path, _paint);
    }

    public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke)
    {
        ApplyStroke(stroke);
        SKPoint deviceCenter = ToDevice(center);
        _canvas.DrawCircle(deviceCenter, DeviceLength(radius), _paint);
    }

    public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke)
    {
        ApplyStroke(stroke);
        SKPoint deviceCenter = ToDevice(center);
        float deviceRadius = DeviceLength(radius);
        var bounds = new SKRect(
            deviceCenter.X - deviceRadius,
            deviceCenter.Y - deviceRadius,
            deviceCenter.X + deviceRadius,
            deviceCenter.Y + deviceRadius);

        // World angles are CCW with Y up; the device space is Y-down, so both the start
        // angle and the sweep are negated to keep the visual orientation correct.
        float startDeg = (float)-GeometryMath.RadiansToDegrees(startAngle);
        float sweepDeg = (float)-GeometryMath.RadiansToDegrees(sweepAngle);

        using var path = new SKPath();
        path.AddArc(bounds, startDeg, sweepDeg);
        _canvas.DrawPath(path, _paint);
    }

    public void DrawText(
        string text,
        Point2D position,
        double height,
        double rotation,
        TextHAlign horizontalAlignment,
        TextVAlign verticalAlignment,
        string? fontFamily,
        double widthFactor,
        in StrokeStyle stroke)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float deviceHeight = DeviceLength(height);
        if (deviceHeight <= 0f)
            return;

        ApplyTypeface(fontFamily);
        _font.Size = deviceHeight;
        _font.ScaleX = widthFactor > 0 ? (float)widthFactor : 1f;
        Color c = stroke.Color;
        _textPaint.Color = new SKColor(c.R, c.G, c.B, c.A);

        float width = _font.MeasureText(text);
        SKFontMetrics metrics = _font.Metrics;

        // Anchor the run around the insertion point. dx/dy are in the (rotated) text frame,
        // where +y is screen-down, so the glyphs always render upright.
        float dx = horizontalAlignment switch
        {
            TextHAlign.Center => -width * 0.5f,
            TextHAlign.Right => -width,
            _ => 0f,
        };
        float dy = verticalAlignment switch
        {
            TextVAlign.Top => -metrics.Ascent,
            TextVAlign.Middle => -(metrics.Ascent + metrics.Descent) * 0.5f,
            TextVAlign.Bottom => -metrics.Descent,
            _ => 0f, // Baseline
        };

        SKPoint device = ToDevice(position);
        _canvas.Save();
        _canvas.Translate(device.X, device.Y);
        // World angles are CCW with Y up; device space is Y-down, so the rotation is negated
        // (same convention as DrawArc) to keep the visual orientation correct.
        _canvas.RotateDegrees((float)-GeometryMath.RadiansToDegrees(rotation));
        _canvas.DrawText(text, dx, dy, _font, _textPaint);
        _canvas.Restore();
    }

    // Typefaces are process-lifetime and meant to be cached/shared, so resolved families are
    // kept in a static cache rather than recreated (and disposed) per draw call.
    private static readonly Dictionary<string, SKTypeface> TypefaceCache = new();

    private string? _currentFamily; // family currently set on _font (null = default typeface)

    private void ApplyTypeface(string? fontFamily)
    {
        string? family = string.IsNullOrEmpty(fontFamily) ? null : fontFamily;
        if (family == _currentFamily)
            return;

        _currentFamily = family;
        _font.Typeface = ResolveTypeface(family);
    }

    private static SKTypeface ResolveTypeface(string? family)
    {
        if (family is null)
            return SKTypeface.Default;

        if (!TypefaceCache.TryGetValue(family, out SKTypeface? typeface))
        {
            typeface = SKTypeface.FromFamilyName(family) ?? SKTypeface.Default;
            TypefaceCache[family] = typeface;
        }

        return typeface;
    }

    private void ApplyStroke(in StrokeStyle stroke)
    {
        Color c = stroke.Color;
        _paint.Color = new SKColor(c.R, c.G, c.B, c.A);
        _paint.StrokeWidth = (float)stroke.Width;
        _paint.PathEffect = DashEffect(stroke.LineType);
    }

    // Dash intervals are expressed in device pixels so a line type stays zoom-stable, and the
    // (immutable) effects are cached for the process lifetime rather than rebuilt per draw.
    private static readonly Dictionary<LineType, SKPathEffect?> DashCache = new();

    private static SKPathEffect? DashEffect(LineType lineType)
    {
        if (lineType == LineType.Solid)
            return null;

        if (!DashCache.TryGetValue(lineType, out SKPathEffect? effect))
        {
            float[] intervals = lineType switch
            {
                LineType.Dashed => new[] { 10f, 6f },
                LineType.Dotted => new[] { 1f, 5f },
                LineType.DashDot => new[] { 12f, 5f, 1f, 5f },
                LineType.DashDotDot => new[] { 12f, 5f, 1f, 5f, 1f, 5f },
                LineType.Center => new[] { 18f, 5f, 5f, 5f },
                LineType.Hidden => new[] { 6f, 4f },
                _ => new[] { 1f },
            };
            effect = SKPathEffect.CreateDash(intervals, 0f);
            DashCache[lineType] = effect;
        }

        return effect;
    }

    private SKPoint ToDevice(Point2D world)
    {
        Point2D screen = _worldToScreen.Transform(world);
        return new SKPoint((float)screen.X, (float)screen.Y);
    }

    private float DeviceLength(double worldLength)
        => (float)_worldToScreen.TransformVector(new Vector2D(worldLength, 0)).Length;

    public void Dispose()
    {
        _paint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
    }
}
