using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class Picker3DTests
{
    private static Camera3D FittedCamera(Model3DObject model)
    {
        var camera = new Camera3D();
        camera.Resize(400, 300);
        camera.ZoomToFit(model.Bounds);
        return camera;
    }

    [Fact]
    public void Pick_CentreOfView_HitsTheObject()
    {
        var box = new Model3DObject(Mesh3D.Box(100, 100, 100));
        Camera3D camera = FittedCamera(box);

        Picker3D.Hit? hit = Picker3D.Pick(camera, new Point2D(200, 150), new[] { box });

        hit.Should().NotBeNull();
        hit!.Value.Object.Should().BeSameAs(box);
    }

    [Fact]
    public void Pick_Corner_MissesEmptySpace()
    {
        var box = new Model3DObject(Mesh3D.Box(20, 20, 20));
        Camera3D camera = FittedCamera(box);

        // Far top-left corner of the viewport is outside the small centred box.
        Picker3D.Pick(camera, new Point2D(2, 2), new[] { box }).Should().BeNull();
    }

    [Fact]
    public void RayFromScreen_CentrePointsTowardTheTarget()
    {
        var camera = new Camera3D();
        camera.Resize(400, 300);
        camera.ZoomToFit(new BoundingBox3D(new Point3D(-50, -50, -50), new Point3D(50, 50, 50)));

        Ray3D ray = camera.RayFromScreen(new Point2D(200, 150));
        // The centre ray direction should align with eye→target.
        Vector3D toTarget = (camera.Target - camera.Eye).Normalized();
        ray.Direction.Dot(toTarget).Should().BeApproximately(1.0, 1e-3);
    }

    [Fact]
    public void SnapVertex_FindsACornerNearTheCursor()
    {
        var box = new Model3DObject(Mesh3D.Box(100, 100, 100));
        Camera3D camera = FittedCamera(box);

        // Project a known corner, then snap from a cursor a few pixels away.
        Point2D corner = camera.Project(box.Mesh.Vertices[0], out _);
        Point3D? snapped = Picker3D.SnapVertex(camera, new Point2D(corner.X + 2, corner.Y + 2), new[] { box }, 10);

        snapped.Should().NotBeNull();
        snapped!.Value.Should().Be(box.Mesh.Vertices[0]);
    }
}
