using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class HatchTests
{
    private static readonly Point2D[] UnitSquare =
    {
        new(0, 0), new(10, 0), new(10, 10), new(0, 10),
    };

    [Fact]
    public void HorizontalHatch_ProducesLinesInsideThePolygon()
    {
        // One horizontal family, spacing 2 → lines at y = 2,4,6,8 inside the 10×10 square.
        var pattern = new HatchPattern("t", new[] { new HatchLineDefinition(0, 2) }, Color.White);

        var segments = HatchGeometry.Generate(UnitSquare, pattern);

        segments.Should().HaveCount(4);
        foreach ((Point2D a, Point2D b) in segments)
        {
            a.Y.Should().Be(b.Y);                       // horizontal
            a.Y.Should().BeInRange(0, 10);
            Math.Min(a.X, b.X).Should().BeApproximately(0, 1e-6);
            Math.Max(a.X, b.X).Should().BeApproximately(10, 1e-6);
        }
    }

    [Fact]
    public void CrossPattern_CombinesBothFamilies()
    {
        var single = new HatchPattern("s", new[] { new HatchLineDefinition(0, 2) }, Color.White);
        var cross = new HatchPattern("c", new[]
        {
            new HatchLineDefinition(0, 2),
            new HatchLineDefinition(90, 2),
        }, Color.White);

        int singleCount = HatchGeometry.Generate(UnitSquare, single).Count;
        int crossCount = HatchGeometry.Generate(UnitSquare, cross).Count;

        crossCount.Should().Be(singleCount * 2);
    }

    [Fact]
    public void DegeneratePolygon_ProducesNothing()
    {
        var pattern = new HatchPattern("t", new[] { new HatchLineDefinition(0, 2) }, Color.White);
        HatchGeometry.Generate(new[] { new Point2D(0, 0), new Point2D(1, 1) }, pattern).Should().BeEmpty();
    }
}
