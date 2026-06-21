using MiniCAD.Core.Geometry;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Projects 3D model edges to 2D screen segments through a <see cref="Camera3D"/>, clipping
/// against the camera's near plane. The result is plain 2D line segments (device pixels) that a
/// backend draws with its existing <c>DrawLine</c> — no new render primitive is needed (#122).
/// </summary>
public static class WireframeProjector
{
    /// <summary>A projected edge in device pixels, tagged with its source object's index.</summary>
    public readonly record struct Segment(Point2D A, Point2D B, int ObjectIndex);

    public static List<Segment> Project(Camera3D camera, IReadOnlyList<Model3DObject> models)
    {
        var segments = new List<Segment>();

        Vector3D viewDir = (camera.Target - camera.Eye).Normalized();
        if (viewDir == Vector3D.Zero)
            return segments;

        // Clip a little in front of the eye so edges crossing behind the camera are trimmed.
        Plane nearPlane = Plane.FromPointNormal(camera.Eye + viewDir * Math.Max(camera.NearPlane, GeometryMath.Epsilon), viewDir);

        for (int i = 0; i < models.Count; i++)
        {
            foreach ((Point3D a, Point3D b) in models[i].WorldMesh().FeatureEdges())
            {
                if (!ClipToFront(nearPlane, a, b, out Point3D ca, out Point3D cb))
                    continue;

                Point2D pa = camera.Project(ca, out _);
                Point2D pb = camera.Project(cb, out _);
                segments.Add(new Segment(pa, pb, i));
            }
        }

        return segments;
    }

    /// <summary>Clips the segment to the front half-space of <paramref name="plane"/>.</summary>
    private static bool ClipToFront(Plane plane, Point3D a, Point3D b, out Point3D ca, out Point3D cb)
    {
        double da = plane.SignedDistance(a);
        double db = plane.SignedDistance(b);
        ca = a;
        cb = b;

        if (da < 0 && db < 0)
            return false; // wholly behind
        if (da >= 0 && db >= 0)
            return true; // wholly in front

        double t = da / (da - db);
        Point3D crossing = a.Lerp(b, t);
        if (da < 0)
            ca = crossing;
        else
            cb = crossing;
        return true;
    }
}
