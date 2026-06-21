using System.Collections.Generic;
using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class RevolveSweepTests
{
    // A small square offset from the Y axis (X in [10,20]) → revolving gives a ring/torus-like solid.
    private static readonly Point2D[] OffsetSquare =
    {
        new(10, 0), new(20, 0), new(20, 10), new(10, 10),
    };

    [Fact]
    public void Revolve_FullTurn_ClosesAroundTheAxis()
    {
        Mesh3D mesh = Revolver.Revolve(OffsetSquare, segments: 32);

        mesh.Vertices.Should().HaveCount(4 * 32);
        // The outer radius is 20, so the body spans roughly [-20,20] in X and Z.
        mesh.Bounds.Max.X.Should().BeApproximately(20, 0.5);
        mesh.Bounds.Min.X.Should().BeApproximately(-20, 0.5);
        mesh.Bounds.Max.Z.Should().BeApproximately(20, 0.5);
        mesh.Bounds.Max.Y.Should().BeApproximately(10, 1e-6);
    }

    [Fact]
    public void Sweep_AlongVerticalPath_BuildsAColumn()
    {
        var profile = new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10) };
        var path = new List<Point3D> { new(0, 0, 0), new(0, 0, 100) };

        Mesh3D mesh = Sweeper.Sweep(profile, path);

        mesh.Vertices.Should().HaveCount(4 * 2);
        mesh.Bounds.Min.Should().Be(new Point3D(0, 0, 0));
        mesh.Bounds.Max.Should().Be(new Point3D(10, 10, 100));
    }
}
