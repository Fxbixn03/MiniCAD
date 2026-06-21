using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class WireframeProjectorTests
{
    private static Camera3D FittedCamera(BoundingBox3D bounds)
    {
        var camera = new Camera3D();
        camera.Resize(400, 300);
        camera.ZoomToFit(bounds);
        return camera;
    }

    [Fact]
    public void Project_Box_YieldsItsEdgesOnScreen()
    {
        var box = new Model3DObject(Mesh3D.Box(100, 100, 100));
        Camera3D camera = FittedCamera(box.Bounds);

        List<WireframeProjector.Segment> segments = WireframeProjector.Project(camera, new[] { box });

        segments.Should().HaveCount(box.Mesh.Edges().Count()); // every visible edge
        segments.Should().OnlyContain(s => double.IsFinite(s.A.X) && double.IsFinite(s.A.Y)
                                        && double.IsFinite(s.B.X) && double.IsFinite(s.B.Y));
        segments.Should().OnlyContain(s => s.ObjectIndex == 0);
    }

    [Fact]
    public void Project_DropsGeometryBehindTheCamera()
    {
        var camera = new Camera3D();
        camera.Resize(400, 300);
        camera.SetStandardView(StandardView.Right); // eye on +X looking toward the origin (down -X)

        // The model sits far on +X, well behind the eye → it must be clipped away entirely.
        var behind = new Model3DObject(Mesh3D.Box(10, 10, 10))
        {
            Transform = Matrix4.Translation(new Vector3D(100000, 0, 0)),
        };

        List<WireframeProjector.Segment> segments = WireframeProjector.Project(camera, new[] { behind });

        segments.Should().BeEmpty();
    }
}
