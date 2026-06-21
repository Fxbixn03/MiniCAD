using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Sections;

namespace MiniCAD.Core.Tests;

public class SectionTests
{
    [Fact]
    public void Generate_CutsBoxIntoItsCrossSectionOutline()
    {
        // A 1000³ box centred at origin (spans ±500 in X/Y/Z).
        var box = new Model3DObject(Mesh3D.Box(1000, 1000, 1000), "Box");

        // Cut along the X axis through y = 0 → cross-section is the 1000×1000 square in X/Z.
        var segments = SectionGenerator.Generate(new[] { box },
            new Point2D(-1000, 0), new Point2D(1000, 0));

        segments.Should().NotBeEmpty();

        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach ((Point2D a, Point2D b) in segments)
        {
            foreach (Point2D p in new[] { a, b })
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
        }

        // X is the distance along the section line from p0 (= world x=-1000), so the box's
        // x∈[-500,500] maps to 500..1500; the cross-section is a 1000×1000 square.
        minX.Should().BeApproximately(500, 1e-6);
        maxX.Should().BeApproximately(1500, 1e-6);
        minY.Should().BeApproximately(-500, 1e-6); // = world Z (elevation)
        maxY.Should().BeApproximately(500, 1e-6);
    }

    [Fact]
    public void Generate_ReturnsNothingWhenLineMissesTheModel()
    {
        var box = new Model3DObject(Mesh3D.Box(100, 100, 100), "Box");
        var segments = SectionGenerator.Generate(new[] { box },
            new Point2D(-1000, 5000), new Point2D(1000, 5000)); // far from the box
        segments.Should().BeEmpty();
    }

    [Fact]
    public void Generate_IsAssociative_FollowsAMovedModel()
    {
        var box = new Model3DObject(Mesh3D.Box(200, 200, 200), "Box");
        var line = (P0: new Point2D(-1000, 0), P1: new Point2D(1000, 0));

        SectionGenerator.Generate(new[] { box }, line.P0, line.P1).Should().NotBeEmpty();

        // Move the model away from the section plane: regenerating yields nothing.
        box.Transform = Matrix4.Translation(new Vector3D(0, 5000, 0));
        SectionGenerator.Generate(new[] { box }, line.P0, line.P1).Should().BeEmpty();
    }
}
