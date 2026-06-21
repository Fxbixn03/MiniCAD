using MiniCAD.Core.Geometry;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Ray-cast picking and snapping in the 3D view (#120): the cursor becomes a ray through the
/// camera, intersected against model triangles for selection and against vertices for snapping.
/// </summary>
public static class Picker3D
{
    public readonly record struct Hit(Model3DObject Object, Point3D Point, double Distance);

    /// <summary>The nearest model object hit by the ray through <paramref name="screen"/>, if any.</summary>
    public static Hit? Pick(Camera3D camera, Point2D screen, IReadOnlyList<Model3DObject> models)
    {
        Ray3D ray = camera.RayFromScreen(screen);
        Hit? best = null;

        foreach (Model3DObject model in models)
        {
            Mesh3D mesh = model.WorldMesh();
            IReadOnlyList<Point3D> v = mesh.Vertices;
            IReadOnlyList<int> idx = mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                if (ray.IntersectTriangle(v[idx[i]], v[idx[i + 1]], v[idx[i + 2]], out double t)
                    && (best is null || t < best.Value.Distance))
                {
                    best = new Hit(model, ray.PointAt(t), t);
                }
            }
        }

        return best;
    }

    /// <summary>The nearest model vertex within <paramref name="tolerancePixels"/> of the cursor, if any.</summary>
    public static Point3D? SnapVertex(Camera3D camera, Point2D screen, IReadOnlyList<Model3DObject> models, double tolerancePixels = 10.0)
    {
        double bestDistance = tolerancePixels;
        Point3D? best = null;

        foreach (Model3DObject model in models)
        {
            foreach (Point3D vertex in model.WorldMesh().Vertices)
            {
                Point2D projected = camera.Project(vertex, out bool inFront);
                if (!inFront)
                    continue;

                double d = projected.DistanceTo(screen);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = vertex;
                }
            }
        }

        return best;
    }
}
