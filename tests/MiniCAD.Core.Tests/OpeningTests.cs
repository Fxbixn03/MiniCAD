using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class OpeningTests
{
    private static bool PointInside(Mesh3D mesh, Point3D p)
    {
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
    public void ArchModelBuilder_SubtractsOverlappingOpeningFromWall()
    {
        // Wall along X, 6000 long, 240 thick, 2500 high at Z 0..2500.
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(6000, 0), thickness: 240, height: 2500);
        // Door opening: axis along the wall x 2500..3500 (1000 door width), width 400 across
        // (spans the 240 thickness), Z 0..2100.
        var door = new OpeningEntity(new Point2D(2500, 0), new Point2D(3500, 0), width: 400, height: 2100);

        var models = ArchModelBuilder.Build(new[] { wall }, new[] { door });
        models.Should().ContainSingle();
        Mesh3D mesh = models[0].WorldMesh();

        // A point in the doorway (low, at the opening) is carved out…
        PointInside(mesh, new Point3D(3000, 0, 1000)).Should().BeFalse();
        // …while solid wall remains away from the opening and above the door head.
        PointInside(mesh, new Point3D(500, 0, 1000)).Should().BeTrue();
        PointInside(mesh, new Point3D(3000, 0, 2300)).Should().BeTrue(); // above 2100 lintel
    }

    [Fact]
    public void ArchModelBuilder_LeavesWallIntactWhenOpeningDoesNotOverlap()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(6000, 0), thickness: 240, height: 2500);
        var far = new OpeningEntity(new Point2D(0, 9000), new Point2D(0, 9001), width: 1000, height: 2100);

        var models = ArchModelBuilder.Build(new[] { wall }, new[] { far });
        Mesh3D mesh = models[0].WorldMesh();

        PointInside(mesh, new Point3D(3000, 0, 1000)).Should().BeTrue(); // untouched
    }

    [Fact]
    public void RoundTrip_PreservesOpening()
    {
        var source = new CadDocument();
        source.AddEntity(new OpeningEntity(new Point2D(1, 0), new Point2D(1, 500),
            width: 800, height: 1300, baseElevation: 900));

        var fileDto = new ProjectFileDto { Name = "T", Document = DocumentMapper.ToDto(source) };
        string json = ProjectSerializer.Serialize(fileDto);
        var target = new CadDocument();
        DocumentMapper.Apply(ProjectSerializer.Deserialize(json).Document, target);

        var opening = target.Entities.OfType<OpeningEntity>().Single();
        opening.Width.Should().BeApproximately(800, 1e-9);
        opening.Height.Should().BeApproximately(1300, 1e-9);
        opening.BaseElevation.Should().BeApproximately(900, 1e-9);
    }
}
