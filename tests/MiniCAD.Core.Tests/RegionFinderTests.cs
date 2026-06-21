using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class RegionFinderTests
{
    private static PolylineEntity Rect(double min, double max)
        => PolylineEntity.Rectangle(new Point2D(min, min), new Point2D(max, max));

    [Fact]
    public void Pick_InInnermost_ReturnsTightestContour()
    {
        var outer = Rect(0, 100);
        var inner = Rect(20, 80);

        Region2D? region = RegionFinder.FindRegion(new[] { outer, inner }, new Point2D(50, 50));

        region.Should().NotBeNull();
        region!.Outer.Should().BeSameAs(inner.Points); // tightest containing contour
        region.Holes.Should().BeEmpty();
    }

    [Fact]
    public void Pick_BetweenContours_ReportsInnerAsHole()
    {
        var outer = Rect(0, 100);
        var inner = Rect(20, 80);

        Region2D? region = RegionFinder.FindRegion(new[] { outer, inner }, new Point2D(10, 10));

        region.Should().NotBeNull();
        region!.Outer.Should().BeSameAs(outer.Points);
        region.Holes.Should().ContainSingle().Which.Should().BeSameAs(inner.Points); // island
    }

    [Fact]
    public void Pick_OutsideEverything_ReturnsNull()
    {
        RegionFinder.FindRegion(new[] { Rect(0, 10) }, new Point2D(50, 50)).Should().BeNull();
    }
}
