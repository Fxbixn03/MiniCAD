using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class CsgTests
{
    // Two unit cubes; B is shifted +0.5 along X so they overlap in [0,0.5]³-ish region.
    private static Mesh3D CubeA() => Mesh3D.Box(1, 1, 1, new Point3D(0, 0, 0));   // [-0.5,0.5]
    private static Mesh3D CubeB() => Mesh3D.Box(1, 1, 1, new Point3D(0.5, 0, 0)); // [0,1]

    private static bool PointInside(Mesh3D mesh, Point3D p)
    {
        // Ray-cast in a generic direction (avoids degenerate hits on axis-aligned edges); odd ⇒ inside.
        var ray = new Ray3D(p, new Vector3D(0.532, 0.213, 0.819));
        int hits = 0;
        IReadOnlyList<Point3D> v = mesh.Vertices;
        IReadOnlyList<int> idx = mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
            if (ray.IntersectTriangle(v[idx[i]], v[idx[i + 1]], v[idx[i + 2]], out double t) && t > 1e-6)
                hits++;
        return hits % 2 == 1;
    }

    [Fact]
    public void Union_BoundsSpanBothInputs()
    {
        Mesh3D result = Csg.Union(CubeA(), CubeB());
        BoundingBox3D b = result.Bounds;
        b.Min.X.Should().BeApproximately(-0.5, 1e-6);
        b.Max.X.Should().BeApproximately(1.0, 1e-6);
        result.Indices.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Union_ContainsPointsFromEitherSolid()
    {
        Mesh3D result = Csg.Union(CubeA(), CubeB());
        PointInside(result, new Point3D(-0.4, 0, 0)).Should().BeTrue();  // only in A
        PointInside(result, new Point3D(0.9, 0, 0)).Should().BeTrue();   // only in B
        PointInside(result, new Point3D(5, 0, 0)).Should().BeFalse();    // outside both
    }

    [Fact]
    public void Intersect_KeepsOnlyTheOverlap()
    {
        Mesh3D result = Csg.Intersect(CubeA(), CubeB());
        PointInside(result, new Point3D(0.25, 0, 0)).Should().BeTrue();  // in both
        PointInside(result, new Point3D(-0.4, 0, 0)).Should().BeFalse(); // only A
        PointInside(result, new Point3D(0.9, 0, 0)).Should().BeFalse();  // only B

        BoundingBox3D b = result.Bounds;
        b.Min.X.Should().BeApproximately(0.0, 1e-6);
        b.Max.X.Should().BeApproximately(0.5, 1e-6);
    }

    [Fact]
    public void Subtract_RemovesTheOverlapFromA()
    {
        Mesh3D result = Csg.Subtract(CubeA(), CubeB());
        PointInside(result, new Point3D(-0.4, 0, 0)).Should().BeTrue();  // part of A kept
        PointInside(result, new Point3D(0.25, 0, 0)).Should().BeFalse(); // carved out by B
    }
}
