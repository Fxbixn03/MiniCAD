using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class OverkillCleanerTests
{
    [Fact]
    public void Clean_RemovesDuplicateLines()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(0, 0), new Point2D(10, 0)); // exact duplicate
        var c = new LineEntity(new Point2D(10, 0), new Point2D(0, 0)); // reversed duplicate

        OverkillResult result = OverkillCleaner.Clean(new IEntity[] { a, b, c });

        result.NetRemoved.Should().Be(2); // three coincident lines collapse to one
    }

    [Fact]
    public void Clean_MergesOverlappingCollinearLines()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(6, 0));
        var b = new LineEntity(new Point2D(4, 0), new Point2D(10, 0)); // overlaps a

        OverkillResult result = OverkillCleaner.Clean(new IEntity[] { a, b });

        result.Added.Should().ContainSingle();
        var merged = (LineEntity)result.Added[0];
        merged.Start.X.Should().BeApproximately(0, 1e-6);
        merged.End.X.Should().BeApproximately(10, 1e-6);
        result.NetRemoved.Should().Be(1);
    }

    [Fact]
    public void Clean_FusesTouchingCollinearLines()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(5, 0));
        var b = new LineEntity(new Point2D(5, 0), new Point2D(10, 0)); // touches a end-to-end

        OverkillResult result = OverkillCleaner.Clean(new IEntity[] { a, b });

        result.Added.Should().ContainSingle();
        result.NetRemoved.Should().Be(1);
    }

    [Fact]
    public void Clean_LeavesDisjointCollinearLinesAlone()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(4, 0));
        var b = new LineEntity(new Point2D(6, 0), new Point2D(10, 0)); // a clear gap between them

        OverkillResult result = OverkillCleaner.Clean(new IEntity[] { a, b });

        result.Removed.Should().BeEmpty();
        result.Added.Should().BeEmpty();
    }

    [Fact]
    public void Clean_DoesNotMergeParallelButOffsetLines()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(0, 5), new Point2D(10, 5)); // parallel, different line

        OverkillResult result = OverkillCleaner.Clean(new IEntity[] { a, b });

        result.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Clean_RemovesDuplicateCirclesAndPoints()
    {
        var entities = new IEntity[]
        {
            new CircleEntity(new Point2D(0, 0), 5),
            new CircleEntity(new Point2D(0, 0), 5),   // duplicate
            new CircleEntity(new Point2D(0, 0), 8),   // different radius, kept
            new PointEntity(new Point2D(1, 1)),
            new PointEntity(new Point2D(1, 1)),        // duplicate
        };

        OverkillResult result = OverkillCleaner.Clean(entities);

        result.NetRemoved.Should().Be(2);
    }

    [Fact]
    public void Clean_RespectsLayerByDefault()
    {
        var layer1 = Guid.NewGuid();
        var layer2 = Guid.NewGuid();
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0)) { LayerId = layer1 };
        var b = new LineEntity(new Point2D(0, 0), new Point2D(10, 0)) { LayerId = layer2 };

        OverkillCleaner.Clean(new IEntity[] { a, b }).Removed.Should().BeEmpty();
        OverkillCleaner.Clean(new IEntity[] { a, b }, new OverkillOptions { IgnoreProperties = true })
            .NetRemoved.Should().Be(1);
    }
}
