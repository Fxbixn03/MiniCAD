using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Volume, surface area and centroid of a closed triangle mesh (#268).</summary>
public readonly record struct MeshMetrics(double Volume, double SurfaceArea, Point3D Centroid, BoundingBox3D Bounds)
{
    /// <summary>
    /// Computes the metrics of <paramref name="mesh"/>: volume via the signed tetrahedra sum from
    /// the origin (divergence theorem), surface area as the sum of triangle areas, and the
    /// volume-weighted centroid. Volume is reported as a positive magnitude regardless of winding.
    /// </summary>
    public static MeshMetrics Compute(Mesh3D mesh)
    {
        IReadOnlyList<Point3D> v = mesh.Vertices;
        IReadOnlyList<int> idx = mesh.Indices;

        double signedVolume6 = 0; // 6× the signed volume
        double area = 0;
        Vector3D centroidAccum = Vector3D.Zero; // Σ (tetVolume6 × tetCentroid)

        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            Vector3D a = Vec(v[idx[i]]), b = Vec(v[idx[i + 1]]), c = Vec(v[idx[i + 2]]);

            double tet6 = a.Dot(b.Cross(c)); // 6× signed volume of (origin, a, b, c)
            signedVolume6 += tet6;
            centroidAccum += (a + b + c) * (tet6 / 4.0); // tetra centroid is (a+b+c+0)/4

            area += 0.5 * (b - a).Cross(c - a).Length;
        }

        double volume = signedVolume6 / 6.0;
        BoundingBox3D bounds = mesh.Bounds;

        Point3D centroid;
        if (System.Math.Abs(signedVolume6) > 1e-9)
        {
            Vector3D c = centroidAccum * (1.0 / signedVolume6);
            centroid = new Point3D(c.X, c.Y, c.Z);
        }
        else
        {
            centroid = bounds.Center;
        }

        return new MeshMetrics(System.Math.Abs(volume), area, centroid, bounds);
    }

    private static Vector3D Vec(Point3D p) => new(p.X, p.Y, p.Z);
}
