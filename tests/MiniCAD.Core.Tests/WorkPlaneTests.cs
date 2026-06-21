using FluentAssertions;
using MiniCAD.Core.Coordinates;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class WorkPlaneTests
{
    [Fact]
    public void TopPlane_IsTheWorldXYPlane()
    {
        WorkPlane top = WorkPlane.Top;
        top.ToWorld(new Point2D(3, 4)).Should().Be(new Point3D(3, 4, 0));
        top.Normal.Should().Be(Vector3D.UnitZ);
    }

    [Fact]
    public void FrontPlane_MapsPlanarYToWorldZ()
    {
        Point3D w = WorkPlane.Front.ToWorld(new Point2D(2, 5)); // X→X, Y→Z
        w.X.Should().BeApproximately(2, 1e-9);
        w.Y.Should().BeApproximately(0, 1e-9);
        w.Z.Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public void ToPlane_IsInverseOfToWorld()
    {
        WorkPlane plane = WorkPlane.FromThreePoints(
            new Point3D(10, 0, 0), new Point3D(10, 5, 0), new Point3D(10, 0, 5)); // a YZ-ish plane at X=10

        var planar = new Point2D(2, 3);
        Point2D round = plane.ToPlane(plane.ToWorld(planar));
        round.X.Should().BeApproximately(2, 1e-9);
        round.Y.Should().BeApproximately(3, 1e-9);
    }

    [Fact]
    public void Document_DefaultsToTopPlane_AndIsSettable()
    {
        var doc = new CadDocument();
        doc.ActiveWorkPlane.Normal.Should().Be(Vector3D.UnitZ);

        doc.ActiveWorkPlane = WorkPlane.Front;
        doc.ActiveWorkPlane.Normal.Y.Should().BeApproximately(-1, 1e-9); // XZ plane normal is ±Y
    }
}
