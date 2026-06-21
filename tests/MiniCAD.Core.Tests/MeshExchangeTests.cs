using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Interop;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class MeshExchangeTests
{
    private static void SameGeometry(Mesh3D original, Mesh3D restored)
    {
        restored.Indices.Count.Should().Be(original.Indices.Count); // same triangle count
        BoundingBox3D a = original.Bounds, b = restored.Bounds;
        b.Min.X.Should().BeApproximately(a.Min.X, 1e-3);
        b.Min.Y.Should().BeApproximately(a.Min.Y, 1e-3);
        b.Min.Z.Should().BeApproximately(a.Min.Z, 1e-3);
        b.Max.X.Should().BeApproximately(a.Max.X, 1e-3);
        b.Max.Y.Should().BeApproximately(a.Max.Y, 1e-3);
        b.Max.Z.Should().BeApproximately(a.Max.Z, 1e-3);
        MeshMetrics.Compute(restored).Volume.Should().BeApproximately(MeshMetrics.Compute(original).Volume, 1.0);
    }

    [Fact]
    public void Stl_RoundTrip_PreservesGeometry()
    {
        Mesh3D box = Mesh3D.Box(1000, 2000, 3000, new Point3D(100, 200, 300));
        Mesh3D restored = MeshExchange.ReadStl(MeshExchange.WriteStl(box));
        SameGeometry(box, restored);
    }

    [Fact]
    public void Obj_RoundTrip_PreservesGeometry()
    {
        Mesh3D box = Mesh3D.Box(1000, 2000, 3000);
        string obj = MeshExchange.WriteObj(new[] { (box, "Quader") });
        Mesh3D restored = MeshExchange.ReadObj(obj);
        SameGeometry(box, restored);
    }

    [Fact]
    public void Combine_ConcatenatesAndOffsetsIndices()
    {
        Mesh3D a = Mesh3D.Box(100, 100, 100, new Point3D(0, 0, 0));
        Mesh3D b = Mesh3D.Box(100, 100, 100, new Point3D(500, 0, 0));
        Mesh3D combined = MeshExchange.Combine(new[] { a, b });

        combined.Vertices.Should().HaveCount(a.Vertices.Count + b.Vertices.Count);
        combined.Indices.Should().HaveCount(a.Indices.Count + b.Indices.Count);
        combined.Bounds.Max.X.Should().BeApproximately(550, 1e-6);
    }

    [Fact]
    public void ReadObj_HandlesFaceIndexFormatsAndPolygons()
    {
        // A quad face with v/vt/vn tokens → fan-triangulated into two triangles.
        string obj = "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1/1/1 2/2/1 3/3/1 4/4/1\n";
        Mesh3D mesh = MeshExchange.ReadObj(obj);
        mesh.Vertices.Should().HaveCount(4);
        mesh.Indices.Should().HaveCount(6); // 2 triangles
    }
}
