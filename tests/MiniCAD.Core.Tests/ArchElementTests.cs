using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class ArchElementTests
{
    private static CadDocument RoundTrip(CadDocument source)
    {
        var fileDto = new ProjectFileDto { Name = "T", Document = DocumentMapper.ToDto(source) };
        string json = ProjectSerializer.Serialize(fileDto);
        var target = new CadDocument();
        DocumentMapper.Apply(ProjectSerializer.Deserialize(json).Document, target);
        return target;
    }

    // ----- Column -----

    [Fact]
    public void Column_RoundHitTest_AndBounds()
    {
        var c = new ColumnEntity(new Point2D(100, 100), round: true, width: 400);
        c.HitTest(new Point2D(100, 100), 0).Should().BeTrue();      // centre
        c.HitTest(new Point2D(305, 100), 0).Should().BeFalse();     // outside radius 200
        c.Bounds.Width.Should().BeApproximately(400, 1e-9);
    }

    [Fact]
    public void ArchModelBuilder_BuildsColumnSolidFromBaseToTop()
    {
        var c = new ColumnEntity(new Point2D(0, 0), round: false, width: 300, depth: 400, height: 2500, baseElevation: 50);
        var models = ArchModelBuilder.Build(new IEntity[] { c });
        models.Should().ContainSingle();
        BoundingBox3D b = models[0].WorldMesh().Bounds;
        b.Min.Z.Should().BeApproximately(50, 1e-6);
        b.Max.Z.Should().BeApproximately(2550, 1e-6);
        (b.Max.X - b.Min.X).Should().BeApproximately(300, 1e-6);
        (b.Max.Y - b.Min.Y).Should().BeApproximately(400, 1e-6);
    }

    [Fact]
    public void Column_RoundTrip()
    {
        var src = new CadDocument();
        src.AddEntity(new ColumnEntity(new Point2D(1, 2), round: true, width: 350, depth: 350, height: 3000, baseElevation: 10));
        var c = RoundTrip(src).Entities.OfType<ColumnEntity>().Single();
        c.Round.Should().BeTrue();
        c.Width.Should().BeApproximately(350, 1e-9);
        c.Height.Should().BeApproximately(3000, 1e-9);
        c.BaseElevation.Should().BeApproximately(10, 1e-9);
    }

    // ----- Slab -----

    [Fact]
    public void ArchModelBuilder_BuildsSlabFromOutline()
    {
        var slab = new SlabEntity(new[]
        {
            new Point2D(0, 0), new Point2D(5000, 0), new Point2D(5000, 4000), new Point2D(0, 4000),
        }, thickness: 200, baseElevation: -200);

        var models = ArchModelBuilder.Build(new IEntity[] { slab });
        models.Should().ContainSingle();
        BoundingBox3D b = models[0].WorldMesh().Bounds;
        b.Min.Z.Should().BeApproximately(-200, 1e-6);
        b.Max.Z.Should().BeApproximately(0, 1e-6);
        (b.Max.X - b.Min.X).Should().BeApproximately(5000, 1e-6);
    }

    [Fact]
    public void Slab_RoundTrip_PreservesOutline()
    {
        var src = new CadDocument();
        src.AddEntity(new SlabEntity(new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) }, 250, 0));
        var s = RoundTrip(src).Entities.OfType<SlabEntity>().Single();
        s.Outline.Should().HaveCount(3);
        s.Thickness.Should().BeApproximately(250, 1e-9);
    }

    // ----- Beam -----

    [Fact]
    public void ArchModelBuilder_BuildsBeamFromAxis()
    {
        var beam = new BeamEntity(new Point2D(0, 0), new Point2D(4000, 0), width: 240, height: 400, baseElevation: 2500);
        var models = ArchModelBuilder.Build(new IEntity[] { beam });
        BoundingBox3D b = models[0].WorldMesh().Bounds;
        b.Min.Z.Should().BeApproximately(2500, 1e-6);
        b.Max.Z.Should().BeApproximately(2900, 1e-6);
        (b.Max.Y - b.Min.Y).Should().BeApproximately(240, 1e-6); // width across the axis
    }

    [Fact]
    public void Beam_RoundTrip()
    {
        var src = new CadDocument();
        src.AddEntity(new BeamEntity(new Point2D(0, 0), new Point2D(3000, 0), 200, 500, 2700));
        var beam = RoundTrip(src).Entities.OfType<BeamEntity>().Single();
        beam.Width.Should().BeApproximately(200, 1e-9);
        beam.Height.Should().BeApproximately(500, 1e-9);
        beam.BaseElevation.Should().BeApproximately(2700, 1e-9);
    }

    [Fact]
    public void ArchModelBuilder_BuildsAllElementsTogether()
    {
        var entities = new IEntity[]
        {
            new WallEntity(new Point2D(0, 0), new Point2D(5000, 0)),
            new ColumnEntity(new Point2D(0, 0)),
            new SlabEntity(new[] { new Point2D(0, 0), new Point2D(5000, 0), new Point2D(5000, 4000), new Point2D(0, 4000) }),
            new BeamEntity(new Point2D(0, 0), new Point2D(5000, 0)),
        };
        ArchModelBuilder.Build(entities).Should().HaveCount(4);
    }
}
