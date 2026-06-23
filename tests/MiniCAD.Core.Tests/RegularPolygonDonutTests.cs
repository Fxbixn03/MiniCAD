using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class RegularPolygonDonutTests
{
    [Fact]
    public void RegularPolygon_Inscribed_PutsVerticesOnTheCircle()
    {
        IReadOnlyList<Point2D> verts = RegularPolygonFactory.Vertices(Point2D.Origin, 1.0, 4, circumscribed: false);

        verts.Should().HaveCount(4);
        verts[0].X.Should().BeApproximately(1, 1e-9);
        verts[0].Y.Should().BeApproximately(0, 1e-9);
        verts[1].X.Should().BeApproximately(0, 1e-9);
        verts[1].Y.Should().BeApproximately(1, 1e-9);
        foreach (Point2D v in verts)
            v.DistanceTo(Point2D.Origin).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void RegularPolygon_Circumscribed_EdgesAreTangentToTheCircle()
    {
        // A square whose edges are tangent to the unit circle has corners at (±1, ±1).
        IReadOnlyList<Point2D> verts = RegularPolygonFactory.Vertices(Point2D.Origin, 1.0, 4, circumscribed: true);

        verts.Should().HaveCount(4);
        foreach (Point2D v in verts)
        {
            Math.Abs(v.X).Should().BeApproximately(1, 1e-9);
            Math.Abs(v.Y).Should().BeApproximately(1, 1e-9);
        }
    }

    [Fact]
    public void RegularPolygon_ClampsSidesToAtLeastThree()
    {
        RegularPolygonFactory.Vertices(Point2D.Origin, 1.0, 2, circumscribed: false).Should().HaveCount(3);
    }

    [Fact]
    public void RegularPolygon_Create_ReturnsClosedPolyline()
    {
        PolylineEntity poly = RegularPolygonFactory.Create(Point2D.Origin, 5.0, 6, circumscribed: false);

        poly.IsClosed.Should().BeTrue();
        poly.Points.Should().HaveCount(6);
    }

    [Fact]
    public void Donut_ProducesTwoConcentricCircles_WithGivenRadii()
    {
        IReadOnlyList<CircleEntity> circles = DonutFactory.Create(new Point2D(2, 3), innerRadius: 3, outerRadius: 7);

        circles.Should().HaveCount(2);
        circles[0].Center.Should().Be(new Point2D(2, 3));
        circles[0].Radius.Should().BeApproximately(7, 1e-9);
        circles[1].Radius.Should().BeApproximately(3, 1e-9);
    }

    [Fact]
    public void Donut_ClampsInnerRadius_WhenNotSmallerThanOuter()
    {
        IReadOnlyList<CircleEntity> circles = DonutFactory.Create(Point2D.Origin, innerRadius: 8, outerRadius: 5);

        circles[0].Radius.Should().BeApproximately(5, 1e-9);
        circles[1].Radius.Should().BeApproximately(3, 1e-9); // 5 * 0.6
    }
}
