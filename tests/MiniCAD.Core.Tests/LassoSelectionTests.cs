using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class LassoSelectionTests
{
    // A 10×10 lasso polygon around the lower-left corner of the world.
    private static readonly IReadOnlyList<Point2D> Box = new[]
    {
        new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10),
    };

    [Fact]
    public void Window_SelectsOnlyFullyEnclosed()
    {
        var inside = new LineEntity(new Point2D(2, 2), new Point2D(8, 8));
        var crossingOut = new LineEntity(new Point2D(5, 5), new Point2D(50, 50));

        List<IEntity> result = LassoSelection.Select(Box, new IEntity[] { inside, crossingOut }, crossing: false);

        result.Should().ContainSingle().Which.Should().BeSameAs(inside);
    }

    [Fact]
    public void Crossing_AlsoSelectsTouched()
    {
        var inside = new LineEntity(new Point2D(2, 2), new Point2D(8, 8));
        var crossingOut = new LineEntity(new Point2D(5, 5), new Point2D(50, 50));
        var fullyOutside = new LineEntity(new Point2D(40, 40), new Point2D(60, 60));

        List<IEntity> result = LassoSelection.Select(
            Box, new IEntity[] { inside, crossingOut, fullyOutside }, crossing: true);

        result.Should().Contain(new IEntity[] { inside, crossingOut });
        result.Should().NotContain(fullyOutside);
    }

    [Fact]
    public void DegeneratePolygon_SelectsNothing()
    {
        var line = new LineEntity(new Point2D(2, 2), new Point2D(8, 8));

        LassoSelection.Select(new[] { new Point2D(0, 0), new Point2D(1, 1) }, new[] { line }, crossing: true)
            .Should().BeEmpty();
    }
}
