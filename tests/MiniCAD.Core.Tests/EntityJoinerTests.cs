using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class EntityJoinerTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void Join_ChainsConnectedLinesIntoOnePolyline()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(10, 0), new Point2D(10, 10));
        var c = new LineEntity(new Point2D(10, 10), new Point2D(0, 10));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { a, b, c }, Tol);

        result.Should().ContainSingle();
        var poly = result[0].Should().BeOfType<PolylineEntity>().Subject;
        poly.IsClosed.Should().BeFalse();
        poly.Points.Should().Equal(
            new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
    }

    [Fact]
    public void Join_StitchesRegardlessOfSegmentDirection()
    {
        // b is reversed relative to the chain direction; should still connect at (10,0).
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(20, 0), new Point2D(10, 0));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { a, b }, Tol);

        result.Should().ContainSingle();
        var line = result[0].Should().BeOfType<LineEntity>().Subject; // collinear → single line
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Should().Be(new Point2D(20, 0));
    }

    [Fact]
    public void Join_CollapsesCollinearRunIntoOneLine()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(5, 0));
        var b = new LineEntity(new Point2D(5, 0), new Point2D(12, 0));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { a, b }, Tol);

        result.Should().ContainSingle().Which.Should().BeOfType<LineEntity>();
    }

    [Fact]
    public void Join_ClosesLoopWhenEndsMeet()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(10, 0), new Point2D(10, 10));
        var c = new LineEntity(new Point2D(10, 10), new Point2D(0, 10));
        var d = new LineEntity(new Point2D(0, 10), new Point2D(0, 0));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { a, b, c, d }, Tol);

        result.Should().ContainSingle();
        var poly = result[0].Should().BeOfType<PolylineEntity>().Subject;
        poly.IsClosed.Should().BeTrue();
        poly.Points.Should().HaveCount(4); // closing duplicate removed
    }

    [Fact]
    public void Join_LeavesDisconnectedSegmentsSeparate()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(50, 50), new Point2D(60, 50));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { a, b }, Tol);

        result.Should().HaveCount(2); // nothing merged
    }

    [Fact]
    public void Join_AbsorbsOpenPolylineIntoTheChain()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) });
        var line = new LineEntity(new Point2D(10, 10), new Point2D(0, 10));

        IReadOnlyList<IEntity> result = EntityJoiner.Join(new IEntity[] { poly, line }, Tol);

        result.Should().ContainSingle();
        ((PolylineEntity)result[0]).Points.Should().HaveCount(4);
    }

    [Fact]
    public void CollectJoinable_IgnoresClosedPolylinesAndOtherTypes()
    {
        var closed = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 3));
        var circle = new CircleEntity(Point2D.Origin, 2);
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));

        EntityJoiner.CollectJoinable(new IEntity[] { closed, circle, line })
            .Should().ContainSingle().Which.Should().BeSameAs(line);
    }
}
