using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Viewing;
using SkiaSharp;

namespace MiniCAD.Renderer.Rendering;

/// <summary>
/// Renders a <see cref="ICadDocument"/> through a <see cref="Viewport"/> onto a SkiaSharp
/// surface. The public <see cref="Render(ICadDocument, Viewport, nint, int, int, int, Color)"/>
/// overload draws straight into a caller-owned pixel buffer (e.g. an Avalonia
/// <c>WriteableBitmap</c>), which keeps every SkiaSharp dependency inside this project.
/// </summary>
public sealed class SkiaSceneRenderer
{
    // Passive (Locked) elements: blended toward a muted blue-gray and faded, so they read as
    // background reference yet stay legible for snapping.
    private static readonly Color PassiveTint = new(120, 130, 145);

    private static StrokeStyle Dim(StrokeStyle stroke)
        => stroke.WithColor(stroke.Color.Lerp(PassiveTint, 0.7).WithAlpha((byte)(stroke.Color.A * 0.6)));

    // Construction geometry (Hilfskonstruktion): tinted toward cyan and dashed so it reads as
    // non-printing helper lines, yet stays snappable.
    private static readonly Color ConstructionTint = new(110, 200, 255);

    private static StrokeStyle Construction(StrokeStyle stroke)
        => stroke.WithColor(stroke.Color.Lerp(ConstructionTint, 0.45)).WithLineType(LineType.Dashed);

    // Blueprint grid: translucent light-blue (logo vertex node #93c5fd) over the navy canvas.
    private static readonly Color GridColor = new(147, 197, 253, 38);
    private static readonly Color XAxisColor = new(190, 92, 96);
    private static readonly Color YAxisColor = new(96, 162, 118);
    private static readonly Color NullPointColor = new(234, 140, 12);

    /// <summary>Set to <c>false</c> to suppress the background reference grid.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Renders into a BGRA8888 premultiplied pixel buffer of the given dimensions.
    /// </summary>
    public void Render(
        ICadDocument document,
        Viewport viewport,
        nint pixelBuffer,
        int width,
        int height,
        int rowBytes,
        Color background,
        IReadOnlyList<OverlayItem>? overlay = null)
    {
        if (width <= 0 || height <= 0 || pixelBuffer == nint.Zero)
            return;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKSurface? surface = SKSurface.Create(info, pixelBuffer, rowBytes);
        if (surface is null)
            return;

        RenderToCanvas(surface.Canvas, document, viewport, background, overlay);
        surface.Canvas.Flush();
    }

    /// <summary>Renders onto an existing canvas. Exposed for export and testing scenarios.</summary>
    public void RenderToCanvas(
        SKCanvas canvas,
        ICadDocument document,
        Viewport viewport,
        Color background,
        IReadOnlyList<OverlayItem>? overlay = null)
    {
        canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));

        var surface = new SkiaRenderSurface(canvas, viewport.WorldToScreen);
        try
        {
            if (ShowGrid)
                DrawGrid(surface, viewport);

            DrawNullPoint(surface, viewport, document);

            // Draw Teilbild by Teilbild in stacking order; within each, only visible entities.
            // Passive (Locked) Teilbilder/layers render dimmed but stay snappable (see SnapEngine).
            foreach (PartialDrawing partialDrawing in document.PartialDrawings)
            {
                if (!partialDrawing.IsVisible)
                    continue;

                bool teilbildPassive = partialDrawing.State == ElementState.Locked;
                surface.ModelScale = partialDrawing.ModelScaleFactor; // scales hatch density per 1:n

                void Draw(IEntity entity)
                {
                    if (entity.PartialDrawingId != partialDrawing.Id)
                        return;

                    Layer? layer = document.FindLayer(entity.LayerId);
                    if (layer is { IsVisible: false })
                        return;

                    StrokeStyle stroke = document.ResolveStroke(entity);
                    if (entity.IsConstruction)
                        stroke = Construction(stroke);
                    if (teilbildPassive || layer is { State: ElementState.Locked })
                        stroke = Dim(stroke);

                    entity.Render(surface, stroke);
                }

                // Raster underlays first so drawn geometry sits on top of the scan.
                foreach (IEntity entity in document.Entities)
                    if (entity is ImageEntity)
                        Draw(entity);
                foreach (IEntity entity in document.Entities)
                    if (entity is not ImageEntity)
                        Draw(entity);
            }

            // Transient overlay (tool previews, selection highlights) on top of everything.
            if (overlay is not null)
            {
                foreach (OverlayItem item in overlay)
                    item.Entity.Render(surface, item.Stroke);
            }
        }
        finally
        {
            surface.Dispose();
        }
    }

    /// <summary>
    /// Draws the document's user-defined origin (Nullpunkt) as a small circle with a cross,
    /// sized in world units so it stays roughly constant on screen at any zoom.
    /// </summary>
    private static void DrawNullPoint(SkiaRenderSurface surface, Viewport viewport, ICadDocument document)
    {
        double scale = Math.Max(viewport.Scale, Viewport.MinScale);
        double r = 9.0 / scale; // ~9 px on screen
        Point2D o = document.CoordinateSystem.Origin.ToPoint2D();

        var stroke = new StrokeStyle(NullPointColor, 1.4);
        surface.DrawCircle(o, r, stroke);
        surface.DrawLine(new Point2D(o.X - r * 1.6, o.Y), new Point2D(o.X + r * 1.6, o.Y), stroke);
        surface.DrawLine(new Point2D(o.X, o.Y - r * 1.6), new Point2D(o.X, o.Y + r * 1.6), stroke);
    }

    private static void DrawGrid(SkiaRenderSurface surface, Viewport viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        // World-space rectangle currently visible (note the Y flip on screen corners).
        Point2D topLeft = viewport.ScreenToWorld(Point2D.Origin);
        Point2D bottomRight = viewport.ScreenToWorld(new Point2D(viewport.Width, viewport.Height));
        double minX = Math.Min(topLeft.X, bottomRight.X);
        double maxX = Math.Max(topLeft.X, bottomRight.X);
        double minY = Math.Min(topLeft.Y, bottomRight.Y);
        double maxY = Math.Max(topLeft.Y, bottomRight.Y);

        double step = GridMetrics.StepFor(viewport.Scale);
        var gridStroke = new StrokeStyle(GridColor, 1.0);

        double startX = Math.Floor(minX / step) * step;
        for (double x = startX; x <= maxX; x += step)
            surface.DrawLine(new Point2D(x, minY), new Point2D(x, maxY), gridStroke);

        double startY = Math.Floor(minY / step) * step;
        for (double y = startY; y <= maxY; y += step)
            surface.DrawLine(new Point2D(minX, y), new Point2D(maxX, y), gridStroke);

        // Emphasize the world axes when they are in view.
        if (minY <= 0 && maxY >= 0)
            surface.DrawLine(new Point2D(minX, 0), new Point2D(maxX, 0), new StrokeStyle(XAxisColor, 1.5));
        if (minX <= 0 && maxX >= 0)
            surface.DrawLine(new Point2D(0, minY), new Point2D(0, maxY), new StrokeStyle(YAxisColor, 1.5));
    }
}
