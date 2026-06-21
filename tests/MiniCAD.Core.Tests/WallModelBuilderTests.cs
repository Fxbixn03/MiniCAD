using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class WallModelBuilderTests
{
    [Fact]
    public void BuildMesh_ExtrudesFootprintFromBaseToTop()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0),
            thickness: 4, height: 2500, baseElevation: 100);

        Mesh3D mesh = WallModelBuilder.BuildMesh(wall);
        BoundingBox3D b = mesh.Bounds;

        b.Min.X.Should().BeApproximately(0, 1e-6);
        b.Max.X.Should().BeApproximately(10, 1e-6);
        b.Min.Y.Should().BeApproximately(-2, 1e-6);
        b.Max.Y.Should().BeApproximately(2, 1e-6);
        b.Min.Z.Should().BeApproximately(100, 1e-6);   // base elevation
        b.Max.Z.Should().BeApproximately(2600, 1e-6);  // base + height
    }

    [Fact]
    public void BuildModel_IsFlaggedDerived()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(5, 0));
        WallModelBuilder.BuildModel(wall).IsDerived.Should().BeTrue();
    }

    [Fact]
    public void DerivedModels_AreExcludedFromPersistence()
    {
        var doc = new CadDocument();
        doc.AddModelObject(new Model3DObject(Mesh3D.Box(1, 1, 1)) { IsDerived = true });
        doc.AddModelObject(new Model3DObject(Mesh3D.Box(1, 1, 1))); // manual, persisted

        DocumentDto dto = DocumentMapper.ToDto(doc);

        dto.Models.Should().ContainSingle(); // only the manual model is saved
    }
}
