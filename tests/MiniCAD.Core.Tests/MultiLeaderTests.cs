using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class MultiLeaderTests
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
    public void RequiresAtLeastOneTip()
    {
        Action create = () => new MultiLeaderEntity(Point2D.Origin, Array.Empty<Point2D>(), "x");

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SnapPoints_IncludeLandingAndAllTips()
    {
        var leader = new MultiLeaderEntity(new Point2D(10, 10),
            new[] { new Point2D(0, 0), new Point2D(0, 5) }, "A1");

        leader.SnapPoints.Should().Contain(new[] { new Point2D(10, 10), new Point2D(0, 0), new Point2D(0, 5) });
    }

    [Fact]
    public void HitTest_OnLeaderLine_Hits()
    {
        var leader = new MultiLeaderEntity(new Point2D(10, 0), new[] { new Point2D(0, 0) }, "A1");

        leader.HitTest(new Point2D(5, 0), 0.1).Should().BeTrue();
        leader.HitTest(new Point2D(5, 5), 0.1).Should().BeFalse();
    }

    [Fact]
    public void Grips_ExposeLandingAndTips()
    {
        var leader = new MultiLeaderEntity(new Point2D(10, 10),
            new[] { new Point2D(0, 0), new Point2D(0, 5) }, "A1");

        leader.GetGrips().Should().HaveCount(3); // landing + 2 tips
    }

    [Fact]
    public void SurvivesRoundTrip()
    {
        var source = new CadDocument();
        source.AddEntity(new MultiLeaderEntity(new Point2D(20, 5),
            new[] { new Point2D(0, 0), new Point2D(2, 8) }, "Pos. 3", textHeight: 10, arrowSize: 9));

        CadDocument restored = RoundTrip(source);

        var leader = restored.Entities.Should().ContainSingle().Which.Should().BeOfType<MultiLeaderEntity>().Subject;
        leader.Landing.Should().Be(new Point2D(20, 5));
        leader.Tips.Should().HaveCount(2);
        leader.Text.Should().Be("Pos. 3");
        leader.TextHeight.Should().BeApproximately(10, 1e-9);
    }
}
