using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Viewing;
using MiniCAD.Renderer.Rendering;
using CoreColor = MiniCAD.Core.Styling.Color;

namespace MiniCAD.App;

/// <summary>Renders a small thumbnail of a block definition for the library palette preview.</summary>
public static class SymbolPreviewRenderer
{
    private static readonly SkiaSceneRenderer Renderer = new() { ShowGrid = false };
    private static readonly CoreColor Background = new(20, 28, 48);

    public static WriteableBitmap? Render(BlockDefinition definition, int width, int height)
    {
        if (definition.Entities.Count == 0 || width <= 0 || height <= 0)
            return null;

        var document = new CadDocument();
        foreach (IEntity entity in definition.Entities)
            document.AddEntity(entity.Clone());

        var viewport = new Viewport();
        viewport.Resize(width, height);
        if (document.GetContentBounds() is { } bounds)
            viewport.ZoomToFit(bounds, 0.8);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            Renderer.Render(document, viewport, framebuffer.Address,
                framebuffer.Size.Width, framebuffer.Size.Height, framebuffer.RowBytes, Background);
        }

        return bitmap;
    }
}
