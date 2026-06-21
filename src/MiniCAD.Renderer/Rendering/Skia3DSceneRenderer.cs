using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Viewing;
using SkiaSharp;

namespace MiniCAD.Renderer.Rendering;

/// <summary>
/// Renders the document's 3D model space as a wireframe through a <see cref="Camera3D"/>
/// (#122). Edges are projected to device pixels by <see cref="WireframeProjector"/> and drawn
/// with the existing 2D <c>DrawLine</c> — the render surface is given an identity world→screen
/// transform so the already-projected pixel coordinates pass through unchanged.
/// </summary>
public sealed class Skia3DSceneRenderer
{
    private static readonly Color HighlightColor = new(255, 170, 40);

    public void Render(ICadDocument document, Camera3D camera, nint pixelBuffer, int width, int height, int rowBytes,
        Color background, Model3DObject? selected = null)
    {
        if (width <= 0 || height <= 0 || pixelBuffer == nint.Zero)
            return;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKSurface? surface = SKSurface.Create(info, pixelBuffer, rowBytes);
        if (surface is null)
            return;

        surface.Canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));

        var drawSurface = new SkiaRenderSurface(surface.Canvas, Matrix2D.Identity);
        try
        {
            IReadOnlyList<Model3DObject> models = document.Models;
            foreach (WireframeProjector.Segment segment in WireframeProjector.Project(camera, models))
            {
                Model3DObject model = models[segment.ObjectIndex];
                bool isSelected = ReferenceEquals(model, selected);
                Color color = isSelected ? HighlightColor : model.Color;
                drawSurface.DrawLine(segment.A, segment.B, new StrokeStyle(color, isSelected ? 2.2 : 1.2));
            }
        }
        finally
        {
            drawSurface.Dispose();
        }

        surface.Canvas.Flush();
    }
}
