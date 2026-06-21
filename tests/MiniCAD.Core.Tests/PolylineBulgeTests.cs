using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class PolylineBulgeTests
{
    [Fact]
    public void BulgeArc_Semicircle_PassesThroughBothEndpoints()
    {
        var a = new Point2D(0, 0);
        var b = new Point2D(10, 0);

        ArcEntity arc = PolylineEntity.BulgeArc(a, b, 1.0)!; // bulge 1 = semicircle

        arc.Radius.Should().BeApproximately(5, 1e-6);
        Point2D start = arc.PointAt(arc.StartAngle);
        start.X.Should().BeApproximately(0, 1e-6);
        start.Y.Should().BeApproximately(0, 1e-6);
        Point2D end = arc.PointAt(arc.StartAngle + arc.SweepAngle);
        end.X.Should().BeApproximately(10, 1e-6);
        end.Y.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void SegmentArc_IsNullForStraightSegments()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });
        poly.SegmentArc(0).Should().BeNull();

        poly.SetBulge(0, 0.5);
        poly.SegmentArc(0).Should().NotBeNull();
    }

    [Fact]
    public void HitTest_FollowsTheArcOfABulgedSegment()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });
        poly.SetBulge(0, 1.0); // semicircle, apex at (5, -5) by the chosen convention

        poly.HitTest(new Point2D(5, -5), 0.1).Should().BeTrue();   // on the arc
        poly.HitTest(new Point2D(5, 0), 0.1).Should().BeFalse();   // on the straight chord, off the arc
    }

    [Fact]
    public void InsertVertex_AddsAPointAndKeepsBulgesAligned()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });

        poly.InsertVertex(0, new Point2D(5, 0));

        poly.Points.Should().Equal(new Point2D(0, 0), new Point2D(5, 0), new Point2D(10, 0));
        poly.Bulges.Should().HaveCount(poly.Points.Count);
    }

    [Fact]
    public void RemoveVertex_DropsThePointAndStraightensTheBridge()
    {
        var poly = new PolylineEntity(new[]
        {
            new Point2D(0, 0), new Point2D(5, 5), new Point2D(10, 0),
        });

        poly.RemoveVertex(1);

        poly.Points.Should().Equal(new Point2D(0, 0), new Point2D(10, 0));
        poly.Bulges.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveVertex_RefusesToGoBelowTwoPoints()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });
        poly.RemoveVertex(0);
        poly.Points.Should().HaveCount(2);
    }

    [Fact]
    public void Transform_Reflection_FlipsBulgeSign()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });
        poly.SetBulge(0, 0.6);

        poly.Transform(Matrix2D.Scaling(1, -1)); // reflect across the X axis

        poly.GetBulge(0).Should().BeApproximately(-0.6, 1e-9);
    }
}
