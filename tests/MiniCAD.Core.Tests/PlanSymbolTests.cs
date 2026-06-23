using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class PlanSymbolTests
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
    public void NorthArrow_DefaultDirection_PointsUp()
    {
        var arrow = new NorthArrowEntity(Point2D.Origin, size: 10);

        arrow.Direction.Should().BeApproximately(Math.PI / 2.0, 1e-9);
        // The tip is above the center for a north-up arrow.
        arrow.SnapPoints.Should().Contain(p => p.Y > 0 && Math.Abs(p.X) < 1e-6);
    }

    [Fact]
    public void NorthArrow_Rotation_TurnsDirection()
    {
        var arrow = new NorthArrowEntity(Point2D.Origin, size: 10, direction: Math.PI / 2.0);

        arrow.Transform(Matrix2D.Rotation(-Math.PI / 2.0)); // rotate 90° clockwise -> points along +X

        arrow.Direction.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void NorthArrow_SurvivesRoundTrip()
    {
        var source = new CadDocument();
        source.AddEntity(new NorthArrowEntity(new Point2D(5, 7), size: 42, direction: 1.1));

        CadDocument restored = RoundTrip(source);

        var arrow = restored.Entities.Should().ContainSingle().Which.Should().BeOfType<NorthArrowEntity>().Subject;
        arrow.Position.Should().Be(new Point2D(5, 7));
        arrow.Size.Should().BeApproximately(42, 1e-9);
        arrow.Direction.Should().BeApproximately(1.1, 1e-9);
    }

    [Fact]
    public void ScaleBar_ReportsRealSpannedLength()
    {
        var bar = new ScaleBarEntity(new Point2D(0, 0), new Point2D(100, 0), divisions: 4);

        bar.Length.Should().BeApproximately(100, 1e-9);
        // A division boundary snap point sits at every quarter.
        bar.SnapPoints.Should().Contain(new Point2D(25, 0));
        bar.SnapPoints.Should().Contain(new Point2D(100, 0));
    }

    [Fact]
    public void ScaleBar_DivisionsClampToAtLeastOne()
    {
        var bar = new ScaleBarEntity(Point2D.Origin, new Point2D(10, 0), divisions: 0);

        bar.Divisions.Should().Be(1);
    }

    [Fact]
    public void ScaleBar_SurvivesRoundTrip()
    {
        var source = new CadDocument();
        source.AddEntity(new ScaleBarEntity(new Point2D(1, 2), new Point2D(51, 2), divisions: 5, height: 8));

        CadDocument restored = RoundTrip(source);

        var bar = restored.Entities.Should().ContainSingle().Which.Should().BeOfType<ScaleBarEntity>().Subject;
        bar.Start.Should().Be(new Point2D(1, 2));
        bar.End.Should().Be(new Point2D(51, 2));
        bar.Divisions.Should().Be(5);
        bar.Height.Should().BeApproximately(8, 1e-9);
    }
}
