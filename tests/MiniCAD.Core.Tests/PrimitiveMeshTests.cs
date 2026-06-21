using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class PrimitiveMeshTests
{
    private static bool IsClosedManifold(Mesh3D mesh)
    {
        // In a closed triangle mesh every undirected edge is shared by exactly two triangles,
        // so counting directed edges, each undirected edge appears twice.
        var count = new Dictionary<(int, int), int>();
        var idx = mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            AddEdge(count, idx[i], idx[i + 1]);
            AddEdge(count, idx[i + 1], idx[i + 2]);
            AddEdge(count, idx[i + 2], idx[i]);
        }
        return count.Values.All(c => c == 2);

        static void AddEdge(Dictionary<(int, int), int> c, int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            c[key] = c.GetValueOrDefault(key) + 1;
        }
    }

    [Fact]
    public void Cylinder_IsClosed_AndSized()
    {
        Mesh3D m = Mesh3D.Cylinder(50, 200, 24);
        m.Bounds.Size.X.Should().BeApproximately(100, 1e-6); // diameter
        m.Bounds.Size.Z.Should().BeApproximately(200, 1e-6);
        IsClosedManifold(m).Should().BeTrue();
    }

    [Fact]
    public void Cone_IsClosed_AndApexAtTop()
    {
        Mesh3D m = Mesh3D.Cone(40, 120, 24);
        m.Bounds.Size.Z.Should().BeApproximately(120, 1e-6);
        m.Bounds.Max.Z.Should().BeApproximately(60, 1e-6);
        IsClosedManifold(m).Should().BeTrue();
    }

    [Fact]
    public void Sphere_IsRoughlyARadiusBall()
    {
        Mesh3D m = Mesh3D.Sphere(100, 24, 16);
        m.Bounds.Size.X.Should().BeApproximately(200, 1.0);
        m.Bounds.Size.Y.Should().BeApproximately(200, 1.0);
        m.Bounds.Size.Z.Should().BeApproximately(200, 1.0);
    }
}
