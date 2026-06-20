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

    private void ApplyStroke(in StrokeStyle stroke)
    {
        Color c = stroke.Color;
        _paint.Color = new SKColor(c.R, c.G, c.B, c.A);
        _paint.StrokeWidth = (float)stroke.Width;
    }

    private SKPoint ToDevice(Point2D world)
    {
        Point2D screen = _worldToScreen.Transform(world);
        return new SKPoint((float)screen.X, (float)screen.Y);
    }

    private float DeviceLength(double worldLength)
        => (float)_worldToScreen.TransformVector(new Vector2D(worldLength, 0)).Length;

    public void Dispose() => _paint.Dispose();
}
