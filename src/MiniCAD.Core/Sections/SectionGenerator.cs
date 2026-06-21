using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Sections;

/// <summary>
/// Generates an associative 2D section from the 3D model space (#93): a vertical cutting plane is
/// defined by a section line in the plan (two XY points) and the world Z axis, and every model mesh
/// is intersected with it. The resulting cut segments are returned in the section's own 2D frame
/// (X = distance along the section line, Y = world elevation), so the section can be regenerated
/// whenever the model changes. The result is purely derived — re-run it to keep it in sync.
/// </summary>
public static class SectionGenerator
{
    /// <summary>
    /// Cuts the models with the vertical plane through the section line <paramref name="p0"/>→
    /// <paramref name="p1"/> and returns the cut profile as line segments in section-local 2D
    /// coordinates (X along the line, Y = elevation).
    /// </summary>
    public static List<(Point2D A, Point2D B)> Generate(IEnumerable<Model3DObject> models, Point2D p0, Point2D p1)
    {
        var segments = new List<(Point2D, Point2D)>();

        Vector2D dir = p1 - p0;
        if (dir.Length <= GeometryMath.Epsilon)
            return segments;
        dir = dir.Normalized();
        var normal = new Vector2D(-dir.Y, dir.X); // in-plane (XY) normal of the cutting plane

        foreach (Model3DObject model in models)
        {
            Mesh3D mesh = model.WorldMesh();
            IReadOnlyList<Point3D> v = mesh.Vertices;
            IReadOnlyList<int> idx = mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
            {
                Point3D a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
                double da = Distance(a, p0, normal), db = Distance(b, p0, normal), dc = Distance(c, p0, normal);

                var crossings = new List<Point3D>(2);
                AddCrossing(crossings, a, b, da, db);
                AddCrossing(crossings, b, c, db, dc);
                AddCrossing(crossings, c, a, dc, da);

                if (crossings.Count == 2)
                    segments.Add((ToLocal(crossings[0], p0, dir), ToLocal(crossings[1], p0, dir)));
            }
        }

        return segments;
    }

    private static double Distance(Point3D p, Point2D origin, Vector2D normal)
        => normal.X * (p.X - origin.X) + normal.Y * (p.Y - origin.Y);

    private static void AddCrossing(List<Point3D> into, Point3D p, Point3D q, double dp, double dq)
    {
        // Edge crosses the plane when the endpoints lie on opposite sides.
        if ((dp > 0 && dq > 0) || (dp < 0 && dq < 0))
            return;
        if (Math.Abs(dp - dq) <= GeometryMath.Epsilon)
            return; // edge parallel to the plane
        double t = dp / (dp - dq);
        if (t < 0 || t > 1)
            return;
        into.Add(p.Lerp(q, t));
    }

    private static Point2D ToLocal(Point3D world, Point2D origin, Vector2D dir)
    {
        double along = (world.X - origin.X) * dir.X + (world.Y - origin.Y) * dir.Y;
        return new Point2D(along, world.Z);
    }
}
