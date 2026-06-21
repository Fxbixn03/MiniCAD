using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class BreakGeometryTests
{
    [Fact]
    public void SplitAt_Line_ProducesTwoSegmentsMeetingAtThePoint()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));

        IReadOnlyList<IEntity> parts = BreakGeometry.SplitAt(line, new Point2D(4, 0));

        parts.Should().HaveCount(2);
        var a = (LineEntity)parts[0];
        var b = (LineEntity)parts[1];
        a.Start.Should().Be(new Point2D(0, 0));
        a.End.Should().Be(new Point2D(4, 0));
        b.Start.Should().Be(new Point2D(4, 0));
        b.End.Should().Be(new Point2D(10, 0));
    }

    [Fact]
    public void SplitAt_PointOnEndpoint_IsANoOp()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        BreakGeometry.SplitAt(line, new Point2D(0, 0)).Should().ContainSingle().Which.Should().BeSameAs(line);
    }

    [Fact]
    public void SplitAt_Arc_SplitsTheSweepInTwo()
    {
        var arc = new ArcEntity(Point2D.Origin, 10, 0, Math.PI); // upper half, CCW 0→180°
        Point2D at = arc.PointAt(Math.PI / 2); // top

        IReadOnlyList<IEntity> parts = BreakGeometry.SplitAt(arc, at);

        parts.Should().HaveCount(2);
        var first = (ArcEntity)parts[0];
        var second = (ArcEntity)parts[1];
        first.SweepAngle.Should().BeApproximately(Math.PI / 2, 1e-6);
        second.StartAngle.Should().BeApproximately(Math.PI / 2, 1e-6);
        second.SweepAngle.Should().BeApproximately(Math.PI / 2, 1e-6);
    }

    [Fact]
    public void SplitAt_OpenPolyline_SplitsAtTheSegment()
    {
        var poly = new PolylineEntity(new[]
        {
            new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10),
        });

        IReadOnlyList<IEntity> parts = BreakGeometry.SplitAt(poly, new Point2D(5, 0));

        parts.Should().HaveCount(2);
        ((PolylineEntity)parts[0]).Points.Should().Equal(new Point2D(0, 0), new Point2D(5, 0));
        ((PolylineEntity)parts[1]).Points.Should().Equal(
            new Point2D(5, 0), new Point2D(10, 0), new Point2D(10, 10));
    }

    [Fact]
    public void SplitAt_ClosedPolyline_OpensItAtThePoint()
    {
        PolylineEntity rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(10, 10));

        IReadOnlyList<IEntity> parts = BreakGeometry.SplitAt(rect, new Point2D(5, 0));

        var open = parts.Should().ContainSingle().Which.Should().BeOfType<PolylineEntity>().Subject;
        open.IsClosed.Should().BeFalse();
        open.Points[0].Should().Be(new Point2D(5, 0));
        open.Points[^1].Should().Be(new Point2D(5, 0)); // walks all the way round back to the break
    }

    [Fact]
    public void BreakBetween_Line_RemovesTheMiddle()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));

        IReadOnlyList<IEntity> parts = BreakGeometry.BreakBetween(line, new Point2D(3, 0), new Point2D(7, 0));

        parts.Should().HaveCount(2);
        ((LineEntity)parts[0]).End.Should().Be(new Point2D(3, 0));
        ((LineEntity)parts[1]).Start.Should().Be(new Point2D(7, 0));
    }

    [Fact]
    public void BreakBetween_Circle_LeavesTheComplementaryArc()
    {
        var circle = new CircleEntity(Point2D.Origin, 10);
        Point2D a = new(10, 0);   // 0°
        Point2D b = new(0, 10);   // 90°

        IReadOnlyList<IEntity> parts = BreakGeometry.BreakBetween(circle, a, b);

        var arc = parts.Should().ContainSingle().Which.Should().BeOfType<ArcEntity>().Subject;
        // Removed the CCW 0°→90° quarter, keeping the remaining 270°.
        arc.StartAngle.Should().BeApproximately(Math.PI / 2, 1e-6);
        arc.SweepAngle.Should().BeApproximately(3 * Math.PI / 2, 1e-6);
    }

    [Fact]
    public void BreakBetween_OpenPolyline_RemovesMiddleLeavingTwoPieces()
    {
        var poly = new PolylineEntity(new[]
        {
            new Point2D(0, 0), new Point2D(10, 0), new Point2D(20, 0), new Point2D(30, 0),
        });

        IReadOnlyList<IEntity> parts = BreakGeometry.BreakBetween(poly, new Point2D(5, 0), new Point2D(25, 0));

        parts.Should().HaveCount(2);
        // First piece ends at the first break, second piece starts at the second break.
        var first = parts[0];
        var second = parts[1];
        (first is LineEntity or PolylineEntity).Should().BeTrue();
        (second is LineEntity or PolylineEntity).Should().BeTrue();
    }

    [Fact]
    public void CanBreak_RejectsText()
    {
        BreakGeometry.CanBreak(new TextEntity(Point2D.Origin, "x")).Should().BeFalse();
        BreakGeometry.CanBreak(new LineEntity(Point2D.Origin, new Point2D(1, 0))).Should().BeTrue();
    }
}
