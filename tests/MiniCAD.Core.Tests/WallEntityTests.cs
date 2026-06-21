using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class WallEntityTests
{
    [Fact]
    public void Footprint_OffsetsTheAxisByHalfThicknessOnBothSides()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0), thickness: 4);

        IReadOnlyList<Point2D> corners = wall.Footprint();

        corners.Should().HaveCount(4);
        // axis along +X → faces at y = ±2
        corners.Should().Contain(new Point2D(0, 2));
        corners.Should().Contain(new Point2D(10, 2));
        corners.Should().Contain(new Point2D(10, -2));
        corners.Should().Contain(new Point2D(0, -2));
    }

    [Fact]
    public void Bounds_CoverTheWholeFootprint()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0), thickness: 4);
        Rect2D b = wall.Bounds;
        b.MinX.Should().BeApproximately(0, 1e-9);
        b.MaxX.Should().BeApproximately(10, 1e-9);
        b.MinY.Should().BeApproximately(-2, 1e-9);
        b.MaxY.Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void HitTest_HitsInsideTheWallBody_AndMissesOutside()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0), thickness: 4);
        wall.HitTest(new Point2D(5, 0), 0.01).Should().BeTrue();    // centre of the body
        wall.HitTest(new Point2D(5, 1.9), 0.01).Should().BeTrue();  // just inside a face
        wall.HitTest(new Point2D(5, 5), 0.01).Should().BeFalse();   // clearly outside
    }

    [Fact]
    public void TopElevation_IsBasePlusHeight()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0), height: 2500, baseElevation: 300);
        wall.TopElevation.Should().Be(2800);
    }

    [Fact]
    public void ClampedThicknessAndHeight_StayPositive()
    {
        var wall = new WallEntity(new Point2D(0, 0), new Point2D(10, 0));
        wall.Thickness = -5;
        wall.Height = 0;
        wall.Thickness.Should().BeGreaterThan(0);
        wall.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RoundTrip_PreservesWall()
    {
        var source = new CadDocument();
        source.AddEntity(new WallEntity(new Point2D(1, 2), new Point2D(11, 2),
            thickness: 300, height: 2750, baseElevation: 120));

        var fileDto = new ProjectFileDto { Name = "T", Document = DocumentMapper.ToDto(source) };
        string json = ProjectSerializer.Serialize(fileDto);
        var target = new CadDocument();
        DocumentMapper.Apply(ProjectSerializer.Deserialize(json).Document, target);

        var wall = target.Entities.OfType<WallEntity>().Single();
        wall.Start.Should().Be(new Point2D(1, 2));
        wall.End.Should().Be(new Point2D(11, 2));
        wall.Thickness.Should().BeApproximately(300, 1e-9);
        wall.Height.Should().BeApproximately(2750, 1e-9);
        wall.BaseElevation.Should().BeApproximately(120, 1e-9);
    }
}
