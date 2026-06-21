using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class Model3DTests
{
    [Fact]
    public void BoxMesh_HasEightVerticesAndTwelveTriangleEdges()
    {
        Mesh3D box = Mesh3D.Box(10, 20, 30);

        box.Vertices.Should().HaveCount(8);
        box.Indices.Should().HaveCount(36); // 12 triangles
        box.Edges().Should().HaveCount(18); // 12 box edges + 6 face diagonals from triangulation
        box.FeatureEdges().Should().HaveCount(12); // only the 12 real cube edges (diagonals dropped)
        box.Bounds.Size.Should().Be(new Vector3D(10, 20, 30));
    }

    [Fact]
    public void FeatureEdges_DropCoplanarSplitsButKeepCreases()
    {
        // A cylinder's faceted side edges (shallow dihedral) drop out; the cap rims (90°) remain,
        // so the feature-edge count is far below the raw triangle-edge count.
        Mesh3D cyl = Mesh3D.Cylinder(50, 100, 24);
        cyl.FeatureEdges().Count().Should().BeLessThan(cyl.Edges().Count() / 2);
        cyl.FeatureEdges().Should().NotBeEmpty();
    }

    [Fact]
    public void ModelObject_WorldBounds_FollowTransform()
    {
        var model = new Model3DObject(Mesh3D.Box(10, 10, 10))
        {
            Transform = Matrix4.Translation(new Vector3D(100, 0, 0)),
        };

        model.Bounds.Center.X.Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void Document_HoldsModelObjects_SeparatelyFromEntities()
    {
        var doc = new CadDocument();
        var model = new Model3DObject(Mesh3D.Box(5, 5, 5), "Würfel");

        doc.AddModelObject(model);

        doc.Models.Should().ContainSingle();
        doc.Entities.Should().BeEmpty(); // the 2D model is untouched
        doc.GetModelBounds().Should().NotBeNull();
    }

    [Fact]
    public void Models_SurvivePersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddModelObject(new Model3DObject(
            Guid.NewGuid(), "Stütze", Mesh3D.Box(30, 30, 300),
            Matrix4.Translation(new Vector3D(50, 60, 0)), Color.Blue));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        Model3DObject model = restored.Models.Should().ContainSingle().Subject;
        model.Name.Should().Be("Stütze");
        model.Mesh.Vertices.Should().HaveCount(8);
        model.Color.Should().Be(Color.Blue);
        model.Bounds.Center.X.Should().BeApproximately(50, 1e-6);
        model.Bounds.Center.Y.Should().BeApproximately(60, 1e-6);
    }
}
