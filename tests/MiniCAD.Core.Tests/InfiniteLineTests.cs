using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class InfiniteLineTests
{
    private static CadDocument RoundTrip(CadDocument source)
    {
        var fileDto = new ProjectFileDto { Name = "Test", Document = DocumentMapper.ToDto(source) };
        string json = ProjectSerializer.Serialize(fileDto);
        ProjectFileDto restored = ProjectSerializer.Deserialize(json);
        var target = new CadDocument();
        DocumentMapper.Apply(restored.Document, target);
        return target;
    }

    [Fact]
    public void ConstructionLine_HitTest_IsInfiniteInBothDirections()
    {
        var line = new ConstructionLineEntity(new Point2D(0, 0), 0.0); // horizontal through origin

        line.HitTest(new Point2D(1000, 0), 0.1).Should().BeTrue();
        line.HitTest(new Point2D(-1000, 0), 0.1).Should().BeTrue();
        line.HitTest(new Point2D(0, 5), 0.1).Should().BeFalse();
    }

    [Fact]
    public void Ray_HitTest_OnlyHitsForwardDirection()
    {
        var ray = new RayEntity(new Point2D(0, 0), 0.0); // points along +X

        ray.HitTest(new Point2D(1000, 0), 0.1).Should().BeTrue();
        ray.HitTest(new Point2D(-1000, 0), 0.1).Should().BeFalse(); // behind the start
    }

    [Fact]
    public void UnboundedEntities_AreExcludedFromContentBounds()
    {
        var doc = new CadDocument();
        doc.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(10, 10)));
        doc.AddEntity(new ConstructionLineEntity(new Point2D(5, 5), Math.PI / 4));

        Rect2D bounds = doc.GetContentBounds()!.Value;

        bounds.MaxX.Should().BeApproximately(10, 1e-6, "the infinite line must not blow up the extent");
    }

    [Fact]
    public void ConstructionLine_IntersectsRect_WhenLineCrossesIt()
    {
        var line = new ConstructionLineEntity(new Point2D(0, 0), Math.PI / 2); // vertical through x=0

        line.IntersectsRect(new Rect2D(-1, 100, 1, 200)).Should().BeTrue();
        line.IntersectsRect(new Rect2D(10, 100, 12, 200)).Should().BeFalse();
    }

    [Fact]
    public void ConstructionLine_SurvivesRoundTrip()
    {
        var source = new CadDocument();
        source.AddEntity(new ConstructionLineEntity(new Point2D(3, 4), 1.2));
        source.AddEntity(new RayEntity(new Point2D(5, 6), 2.1));

        CadDocument restored = RoundTrip(source);

        restored.Entities.Should().HaveCount(2);
        var xline = restored.Entities[0].Should().BeOfType<ConstructionLineEntity>().Subject;
        xline.BasePoint.Should().Be(new Point2D(3, 4));
        xline.DirectionAngle.Should().BeApproximately(1.2, 1e-9);
        restored.Entities[1].Should().BeOfType<RayEntity>();
    }
}
