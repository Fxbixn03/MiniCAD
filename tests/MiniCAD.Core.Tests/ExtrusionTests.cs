using System.Collections.Generic;
using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class ExtrusionTests
{
    private static readonly Point2D[] Square =
    {
        new(0, 0), new(10, 0), new(10, 10), new(0, 10),
    };

    [Fact]
    public void Extrude_Square_ProducesAPrismWithCaps()
    {
        Mesh3D mesh = Extruder.Extrude(Square, baseZ: 5, height: 50);

        mesh.Vertices.Should().HaveCount(8);               // 4 bottom + 4 top
        mesh.Indices.Should().HaveCount(36);               // 4 side quads (24) + 2 caps × 2 tris (12)
        mesh.Bounds.Min.Should().Be(new Point3D(0, 0, 5));
        mesh.Bounds.Max.Should().Be(new Point3D(10, 10, 55));
    }

    [Fact]
    public void Triangulator_ConcaveL_ProducesValidTriangleCount()
    {
        // An L-shaped (concave) hexagon → n-2 = 4 triangles.
        var lShape = new List<Point2D>
        {
            new(0, 0), new(20, 0), new(20, 10), new(10, 10), new(10, 20), new(0, 20),
        };

        List<int> tris = PolygonTriangulator.Triangulate(lShape);
        tris.Count.Should().Be(4 * 3);
    }

    [Fact]
    public void ProfileExtractor_ReadsClosedPolylineAndCircle()
    {
        var poly = new PolylineEntity(Square, isClosed: true);
        ProfileExtractor.FromEntity(poly).Should().HaveCount(4);

        ProfileExtractor.FromEntity(new CircleEntity(Point2D.Origin, 5)).Should().NotBeNull();
        ProfileExtractor.FromEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1))).Should().BeNull();
    }
}
