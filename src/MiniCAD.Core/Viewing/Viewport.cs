using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Viewing;

/// <summary>
/// Maps between world coordinates and screen (device-pixel) coordinates. The Y axis is
/// flipped so that world-up corresponds to screen-up, matching CAD conventions. The class
/// is pure math — no UI dependency — and raises <see cref="Changed"/> whenever the view
/// moves so a host control can repaint.
/// </summary>
public sealed class Viewport
{
    public const double MinScale = 1e-4;
    public const double MaxScale = 1e6;

    /// <summary>Pixels per world unit. Always within [<see cref="MinScale"/>, <see cref="MaxScale"/>].</summary>
    public double Scale { get; private set; } = 1.0;

    /// <summary>The world point shown at the center of the viewport.</summary>
    public Point2D Center { get; private set; } = Point2D.Origin;

    /// <summary>Viewport width in device pixels.</summary>
    public double Width { get; private set; }

    /// <summary>Viewport height in device pixels.</summary>
    public double Height { get; private set; }

    public event EventHandler? Changed;

    /// <summary>The world → screen affine transform implied by the current state.</summary>
    public Matrix2D WorldToScreen => new(
        Scale, 0.0,
        0.0, -Scale,
        Width * 0.5 - Center.X * Scale,
        Height * 0.5 + Center.Y * Scale);

    public Point2D WorldToScreenPoint(Point2D world) => WorldToScreen.Transform(world);

    public Point2D ScreenToWorld(Point2D screen) => new(
        Center.X + (screen.X - Width * 0.5) / Scale,
        Center.Y - (screen.Y - Height * 0.5) / Scale);

    /// <summary>Updates the device-pixel size; raises <see cref="Changed"/> only if it actually changed.</summary>
    public void Resize(double width, double height)
    {
        if (width == Width && height == Height)
            return;

        Width = width;
        Height = height;
        OnChanged();
    }

    /// <summary>Pans by a screen-space delta (e.g. a mouse drag).</summary>
    public void PanByScreenDelta(double deltaX, double deltaY)
    {
        Center = new Point2D(Center.X - deltaX / Scale, Center.Y + deltaY / Scale);
        OnChanged();
    }

    /// <summary>Zooms by <paramref name="factor"/> while keeping the world point under <paramref name="screenAnchor"/> fixed.</summary>
    public void ZoomAt(Point2D screenAnchor, double factor)
    {
        Point2D worldAnchor = ScreenToWorld(screenAnchor);
        Scale = GeometryMath.Clamp(Scale * factor, MinScale, MaxScale);
        Center = new Point2D(
            worldAnchor.X - (screenAnchor.X - Width * 0.5) / Scale,
            worldAnchor.Y + (screenAnchor.Y - Height * 0.5) / Scale);
        OnChanged();
    }

    /// <summary>Frames <paramref name="bounds"/> within the viewport, leaving a small margin.</summary>
    public void ZoomToFit(Rect2D bounds, double fillFactor = 0.9)
    {
        if (Width <= 0 || Height <= 0)
            return;

        double boundsWidth = Math.Max(bounds.Width, 1e-6);
        double boundsHeight = Math.Max(bounds.Height, 1e-6);
        double scale = Math.Min(Width / boundsWidth, Height / boundsHeight) * fillFactor;

        Scale = GeometryMath.Clamp(scale, MinScale, MaxScale);
        Center = bounds.Center;
        OnChanged();
    }

    public void Reset()
    {
        Scale = 1.0;
        Center = Point2D.Origin;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
