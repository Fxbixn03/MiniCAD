using System;
using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class Camera3DTests
{
    private static Camera3D FittedCamera()
    {
        var camera = new Camera3D();
        camera.Resize(400, 300);
        camera.ZoomToFit(new BoundingBox3D(new Point3D(-50, -50, -50), new Point3D(50, 50, 50)));
        return camera;
    }

    [Fact]
    public void Project_TargetCentre_LandsNearScreenCentre()
    {
        Camera3D camera = FittedCamera();

        Point2D screen = camera.Project(camera.Target, out bool inFront);

        inFront.Should().BeTrue();
        screen.X.Should().BeApproximately(200, 1.0); // ~centre of 400×300
        screen.Y.Should().BeApproximately(150, 1.0);
    }

    [Fact]
    public void Orbit_MovesTheEye_AroundTheTarget()
    {
        var camera = new Camera3D();
        Point3D before = camera.Eye;
        double distanceBefore = (before - camera.Target).Length;

        camera.Orbit(0.5, 0.0);

        camera.Eye.Should().NotBe(before);
        (camera.Eye - camera.Target).Length.Should().BeApproximately(distanceBefore, 1e-6); // orbit keeps distance
    }

    [Fact]
    public void Pitch_IsClampedBelowVertical()
    {
        var camera = new Camera3D();
        camera.Orbit(0, 10.0); // way past vertical
        camera.Pitch.Should().BeLessThan(Math.PI / 2);
    }

    [Fact]
    public void TopView_LooksStraightDown_SoZBarelyAffectsScreenXY()
    {
        var camera = new Camera3D { Mode = ProjectionMode.Orthographic };
        camera.Resize(400, 400);
        camera.SetStandardView(StandardView.Top);
        camera.ZoomToFit(new BoundingBox3D(new Point3D(-50, -50, 0), new Point3D(50, 50, 0)));

        // Looking down Z: raising a point in Z should not move its projected X/Y much.
        Point2D a = camera.Project(new Point3D(10, 10, 0), out _);
        Point2D b = camera.Project(new Point3D(10, 10, 40), out _);
        a.X.Should().BeApproximately(b.X, 1.0);
        a.Y.Should().BeApproximately(b.Y, 1.0);
    }

    [Fact]
    public void Zoom_ReducesDistanceWhenZoomingIn()
    {
        var camera = new Camera3D();
        double before = camera.Distance;
        camera.Zoom(2.0);
        camera.Distance.Should().BeApproximately(before / 2.0, 1e-6);
    }
}
