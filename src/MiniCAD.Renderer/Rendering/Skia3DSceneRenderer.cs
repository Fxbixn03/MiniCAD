using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Viewing;
using SkiaSharp;

namespace MiniCAD.Renderer.Rendering;

/// <summary>
/// Renders the document's 3D model space through a <see cref="Camera3D"/> in one of three modes
/// (#122/#92/#123): wireframe, hidden-line (faces occlude edges) or flat-shaded with depth
/// sorting and a light. Everything is projected to device pixels and drawn with the existing 2D
/// primitives on a surface with an identity world→screen transform.
/// </summary>
public sealed class Skia3DSceneRenderer
{
    private static readonly Color HighlightColor = new(255, 170, 40);

    // Fixed world light direction (from the upper-front) for flat shading.
    private static readonly Vector3D Light = new Vector3D(0.4, 0.5, 1.0).Normalized();

    public void Render(ICadDocument document, Camera3D camera, nint pixelBuffer, int width, int height, int rowBytes,
        Color background, Model3DObject? selected = null, Render3DMode mode = Render3DMode.Wireframe,
        bool showGround = true)
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
            if (showGround)
                DrawGroundAndAxes(drawSurface, camera, document);

            IReadOnlyList<Model3DObject> models = document.Models;
            if (mode == Render3DMode.Wireframe)
                RenderWireframe(drawSurface, camera, models, selected);
            else
                RenderSolid(drawSurface, camera, models, selected, background, mode);
        }
        finally
        {
            drawSurface.Dispose();
        }

        surface.Canvas.Flush();
    }

    // ----- Ground grid + axis triad (orientation aids, Allplan-style) -----

    private static readonly Color GridColor = new(42, 50, 70);
    private static readonly Color AxisX = new(205, 95, 95);
    private static readonly Color AxisY = new(95, 180, 95);
    private static readonly Color AxisZ = new(105, 135, 235);

    private static void DrawGroundAndAxes(SkiaRenderSurface surface, Camera3D camera, ICadDocument document)
    {
        double extent;
        double centerX = 0, centerY = 0;
        if (document.GetModelBounds() is { } b)
        {
            extent = Math.Max(Math.Max(b.Size.X, b.Size.Y), 1.0);
            centerX = b.Center.X;
            centerY = b.Center.Y;
        }
        else
        {
            extent = Math.Max(camera.Distance, 1.0);
        }

        double step = NiceStep(extent / 8.0);
        const int n = 12;
        double half = n * step;
        double cx = Math.Round(centerX / step) * step;
        double cy = Math.Round(centerY / step) * step;

        var grid = new StrokeStyle(GridColor, 1.0);
        for (int i = -n; i <= n; i++)
        {
            double x = cx + i * step;
            DrawWorldLine(surface, camera, new Point3D(x, cy - half, 0), new Point3D(x, cy + half, 0), grid);
            double y = cy + i * step;
            DrawWorldLine(surface, camera, new Point3D(cx - half, y, 0), new Point3D(cx + half, y, 0), grid);
        }

        // Axis triad at the world origin (X red, Y green, Z blue).
        DrawWorldLine(surface, camera, Point3D.Origin, new Point3D(half, 0, 0), new StrokeStyle(AxisX, 1.8));
        DrawWorldLine(surface, camera, Point3D.Origin, new Point3D(0, half, 0), new StrokeStyle(AxisY, 1.8));
        DrawWorldLine(surface, camera, Point3D.Origin, new Point3D(0, 0, half * 0.5), new StrokeStyle(AxisZ, 1.8));
    }

    private static void DrawWorldLine(SkiaRenderSurface surface, Camera3D camera, Point3D a, Point3D b, in StrokeStyle stroke)
    {
        Point2D pa = camera.Project(a, out bool fa);
        Point2D pb = camera.Project(b, out bool fb);
        if (!fa || !fb)
            return; // skip lines crossing behind the camera
        surface.DrawLine(pa, pb, stroke);
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0)
            return 1.0;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double nice = norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10;
        return nice * mag;
    }

    private static void RenderWireframe(SkiaRenderSurface surface, Camera3D camera, IReadOnlyList<Model3DObject> models, Model3DObject? selected)
    {
        foreach (WireframeProjector.Segment segment in WireframeProjector.Project(camera, models))
        {
            Model3DObject model = models[segment.ObjectIndex];
            bool isSelected = ReferenceEquals(model, selected);
            Color color = isSelected ? HighlightColor : model.Color;
            surface.DrawLine(segment.A, segment.B, new StrokeStyle(color, isSelected ? 2.2 : 1.2));
        }
    }

    private readonly record struct Face(Point2D A, Point2D B, Point2D C, double Depth, Color Color, bool Selected);

    private static void RenderSolid(SkiaRenderSurface surface, Camera3D camera, IReadOnlyList<Model3DObject> models,
        Model3DObject? selected, Color background, Render3DMode mode)
    {
        Matrix4 view = camera.ViewMatrix;
        var faces = new List<Face>();

        foreach (Model3DObject model in models)
        {
            bool isSelected = ReferenceEquals(model, selected);
            Mesh3D mesh = model.WorldMesh();
            IReadOnlyList<Point3D> v = mesh.Vertices;
            IReadOnlyList<int> idx = mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                Point3D wa = v[idx[i]], wb = v[idx[i + 1]], wc = v[idx[i + 2]];
                Point2D pa = camera.Project(wa, out bool fa);
                Point2D pb = camera.Project(wb, out bool fb);
                Point2D pc = camera.Project(wc, out bool fc);
                if (!fa || !fb || !fc)
                    continue; // skip triangles crossing behind the camera

                double depth = (view.Transform(wa).Z + view.Transform(wb).Z + view.Transform(wc).Z) / 3.0;
                Color color = mode == Render3DMode.HiddenLine
                    ? background
                    : Shade(isSelected ? HighlightColor : model.Color, wa, wb, wc);
                faces.Add(new Face(pa, pb, pc, depth, color, isSelected));
            }
        }

        faces.Sort((x, y) => y.Depth.CompareTo(x.Depth)); // back (large z) to front

        foreach (Face f in faces)
        {
            var poly = new[] { f.A, f.B, f.C };
            surface.DrawFilledPolygon(poly, FillStyle.Solid(f.Color));
            if (mode == Render3DMode.HiddenLine)
            {
                var stroke = new StrokeStyle(f.Selected ? HighlightColor : new Color(210, 220, 235), f.Selected ? 2.0 : 1.0);
                surface.DrawPolyline(poly, closed: true, stroke);
            }
        }
    }

    private static Color Shade(Color baseColor, Point3D a, Point3D b, Point3D c)
    {
        Vector3D normal = (b - a).Cross(c - a).Normalized();
        double factor = 0.35 + 0.65 * Math.Abs(normal.Dot(Light));
        return new Color(
            (byte)Math.Clamp(baseColor.R * factor, 0, 255),
            (byte)Math.Clamp(baseColor.G * factor, 0, 255),
            (byte)Math.Clamp(baseColor.B * factor, 0, 255));
    }
}
