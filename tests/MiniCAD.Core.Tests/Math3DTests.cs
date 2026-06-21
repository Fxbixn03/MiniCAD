using System;
using FluentAssertions;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class Math3DTests
{
    private static void ShouldApproximate(Point3D actual, double x, double y, double z, double tol = 1e-9)
    {
        actual.X.Should().BeApproximately(x, tol);
        actual.Y.Should().BeApproximately(y, tol);
        actual.Z.Should().BeApproximately(z, tol);
    }

    [Fact]
    public void Vector3D_CrossAndDot_AreCorrect()
    {
        Vector3D.UnitX.Cross(Vector3D.UnitY).Should().Be(Vector3D.UnitZ);
        Vector3D.UnitX.Dot(Vector3D.UnitY).Should().Be(0);
        new Vector3D(3, 4, 0).Length.Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public void Matrix4_TranslationAndRotation_ComposeInRowVectorOrder()
    {
        // Apply translation first, then a 90° rotation about Z (a*b applies a first).
        Matrix4 m = Matrix4.Translation(new Vector3D(1, 0, 0)) * Matrix4.RotationZ(Math.PI / 2);
        Point3D p = m.Transform(Point3D.Origin);
        ShouldApproximate(p, 0, 1, 0); // (1,0,0) rotated 90° about Z → (0,1,0)
    }

    [Fact]
    public void Matrix4_RotationZ_RotatesXTowardsY()
    {
        Point3D p = Matrix4.RotationZ(Math.PI / 2).Transform(new Point3D(1, 0, 0));
        ShouldApproximate(p, 0, 1, 0);
    }

    [Fact]
    public void Matrix4_Inverse_RoundTripsAPoint()
    {
        Matrix4 m = Matrix4.Translation(new Vector3D(5, -2, 3)) * Matrix4.RotationY(0.7) * Matrix4.Scaling(2);
        m.TryInvert(out Matrix4 inv).Should().BeTrue();

        var p = new Point3D(3, 4, 5);
        Point3D round = inv.Transform(m.Transform(p));
        ShouldApproximate(round, 3, 4, 5, 1e-6);
    }

    [Fact]
    public void Quaternion_FromAxisAngle_RotatesLikeMatrix()
    {
        Quaternion q = Quaternion.FromAxisAngle(Vector3D.UnitZ, Math.PI / 2);
        Vector3D rotated = q.Rotate(Vector3D.UnitX);
        rotated.X.Should().BeApproximately(0, 1e-9);
        rotated.Y.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Plane_SignedDistanceAndProjection()
    {
        var plane = Plane.FromPointNormal(new Point3D(0, 0, 5), Vector3D.UnitZ);
        plane.SignedDistance(new Point3D(2, 3, 8)).Should().BeApproximately(3, 1e-9);
        ShouldApproximate(plane.Project(new Point3D(2, 3, 8)), 2, 3, 5);
    }

    [Fact]
    public void Plane_IntersectRay_FindsTheHit()
    {
        var plane = Plane.FromPointNormal(new Point3D(0, 0, 10), Vector3D.UnitZ);
        var ray = new Ray3D(Point3D.Origin, Vector3D.UnitZ);

        plane.IntersectRay(ray, out double t, out Point3D hit).Should().BeTrue();
        t.Should().BeApproximately(10, 1e-9);
        ShouldApproximate(hit, 0, 0, 10);
    }

    [Fact]
    public void Ray_IntersectsTriangle()
    {
        var a = new Point3D(0, 0, 5);
        var b = new Point3D(4, 0, 5);
        var c = new Point3D(0, 4, 5);
        var ray = new Ray3D(new Point3D(1, 1, 0), Vector3D.UnitZ);

        ray.IntersectTriangle(a, b, c, out double t).Should().BeTrue();
        t.Should().BeApproximately(5, 1e-9);

        var miss = new Ray3D(new Point3D(3, 3, 0), Vector3D.UnitZ); // outside the triangle
        miss.IntersectTriangle(a, b, c, out _).Should().BeFalse();
    }

    [Fact]
    public void BoundingBox3D_FromPoints_AndUnion()
    {
        var box = BoundingBox3D.FromPoints(new[] { new Point3D(1, 2, 3), new Point3D(-1, 5, 0) });
        box.Min.Should().Be(new Point3D(-1, 2, 0));
        box.Max.Should().Be(new Point3D(1, 5, 3));
        box.Corners().Should().HaveCount(8);
        box.Contains(new Point3D(0, 3, 1)).Should().BeTrue();
    }

    [Fact]
    public void Perspective_ProjectsAForwardPointToTheCenter()
    {
        Matrix4 view = Matrix4.CreateLookAt(new Point3D(0, 0, -10), Point3D.Origin, Vector3D.UnitY);
        Matrix4 proj = Matrix4.CreatePerspective(Math.PI / 3, 1.0, 0.1, 100);
        Matrix4 vp = view * proj;

        // A point straight ahead projects to the screen centre (x=y=0 in NDC).
        Point3D clip = vp.TransformWithW(Point3D.Origin, out double w);
        w.Should().BeGreaterThan(0);
        (clip.X / w).Should().BeApproximately(0, 1e-9);
        (clip.Y / w).Should().BeApproximately(0, 1e-9);
    }
}
