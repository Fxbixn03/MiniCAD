using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class MultilineGeneratorTests
{
    private static readonly IReadOnlyList<Point2D> Horizontal = new[] { new Point2D(0, 0), new Point2D(10, 0) };

    [Fact]
    public void Center_ProducesTwoRailsAtHalfWidthEachSide()
    {
        IReadOnlyList<PolylineEntity> rails = MultilineGenerator.Generate(Horizontal, closed: false, width: 2, MultilineJustification.Center);

        rails.Should().HaveCount(2);
        rails[0].Points.Should().BeEquivalentTo(new[] { new Point2D(0, 1), new Point2D(10, 1) });
        rails[1].Points.Should().BeEquivalentTo(new[] { new Point2D(0, -1), new Point2D(10, -1) });
    }

    [Fact]
    public void Left_KeepsPathAsOneRail()
    {
        IReadOnlyList<PolylineEntity> rails = MultilineGenerator.Generate(Horizontal, closed: false, width: 2, MultilineJustification.Left);

        rails[0].Points.Should().BeEquivalentTo(new[] { new Point2D(0, 0), new Point2D(10, 0) });
        rails[1].Points.Should().BeEquivalentTo(new[] { new Point2D(0, -2), new Point2D(10, -2) });
    }

    [Fact]
    public void Corner_IsMitered()
    {
        // L-shaped path: (0,0) -> (10,0) -> (10,10), centered width 2.
        var path = new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) };

        IReadOnlyList<PolylineEntity> rails = MultilineGenerator.Generate(path, closed: false, width: 2, MultilineJustification.Center);

        // Outer rail corner mitres inward to (9,1); inner rail to (11,-1).
        rails[0].Points[1].X.Should().BeApproximately(9, 1e-9);
        rails[0].Points[1].Y.Should().BeApproximately(1, 1e-9);
        rails[1].Points[1].X.Should().BeApproximately(11, 1e-9);
        rails[1].Points[1].Y.Should().BeApproximately(-1, 1e-9);
    }

    [Fact]
    public void RailsHaveSameVertexCountAsPath()
    {
        var path = new[] { new Point2D(0, 0), new Point2D(5, 0), new Point2D(5, 5), new Point2D(0, 5) };

        IReadOnlyList<PolylineEntity> rails = MultilineGenerator.Generate(path, closed: false, width: 1, MultilineJustification.Center);

        rails[0].Points.Should().HaveCount(4);
        rails[1].Points.Should().HaveCount(4);
    }
}
