using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class EntityTests
{
    [Fact]
    public void Line_Bounds_SpanEndpoints()
    {
        var line = new LineEntity(new Point2D(1, 5), new Point2D(4, 2));

        line.Bounds.Should().Be(new Rect2D(1, 2, 4, 5));
    }

    [Fact]
    public void Line_HitTest_RespectsTolerance()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));

        line.HitTest(new Point2D(5, 0.05), 0.1).Should().BeTrue();
        line.HitTest(new Point2D(5, 1), 0.1).Should().BeFalse();
    }

    [Fact]
    public void Line_Transform_TranslatesEndpoints()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(2, 0));

        line.Transform(Matrix2D.Translation(3, 4));

        line.Start.Should().Be(new Point2D(3, 4));
        line.End.Should().Be(new Point2D(5, 4));
    }

    [Fact]
    public void Circle_HitTest_MatchesRing_NotInterior()
    {
        var circle = new CircleEntity(Point2D.Origin, 5);

        circle.HitTest(new Point2D(5, 0), 0.01).Should().BeTrue();
        circle.HitTest(Point2D.Origin, 0.01).Should().BeFalse();
    }

    [Fact]
    public void Circle_Transform_ScalesRadiusUniformly()
    {
        var circle = new CircleEntity(Point2D.Origin, 2);

        circle.Transform(Matrix2D.Scaling(3, 3));

        circle.Radius.Should().BeApproximately(6, 1e-9);
    }

    [Fact]
    public void Polyline_Rectangle_IsClosed_WithExpectedBounds()
    {
        PolylineEntity rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 3));

        rect.IsClosed.Should().BeTrue();
        rect.Points.Should().HaveCount(4);
        rect.Bounds.Should().Be(new Rect2D(0, 0, 4, 3));
    }

    [Fact]
    public void Polyline_Closed_HitTest_FindsClosingSegment()
    {
        PolylineEntity rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 4));

        // Point on the left edge, which only exists because the polyline is closed.
        rect.HitTest(new Point2D(0, 2), 0.01).Should().BeTrue();
    }

    [Fact]
    public void Clone_ProducesIndependentCopy_WithNewId()
    {
        var original = new LineEntity(new Point2D(0, 0), new Point2D(1, 1)) { LayerId = Guid.NewGuid() };

        var clone = (LineEntity)original.Clone();
        clone.Transform(Matrix2D.Translation(10, 0));

        clone.Id.Should().NotBe(original.Id);
        clone.LayerId.Should().Be(original.LayerId);
        original.Start.Should().Be(new Point2D(0, 0));
    }

    [Fact]
    public void Arc_HitTest_RespectsSweep()
    {
        // Quarter arc from 0° to 90°.
        var arc = new ArcEntity(Point2D.Origin, 5, 0, GeometryMath.DegreesToRadians(90));

        arc.HitTest(new Point2D(0, 5), 0.01).Should().BeTrue();   // 90°, inside sweep
        arc.HitTest(new Point2D(-5, 0), 0.01).Should().BeFalse(); // 180°, outside sweep
    }
}
