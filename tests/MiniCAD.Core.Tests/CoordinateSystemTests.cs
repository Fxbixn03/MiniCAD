using FluentAssertions;
using MiniCAD.Core.Coordinates;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class CoordinateSystemTests
{
    [Fact]
    public void ToLocal_IsRelativeToOrigin()
    {
        var system = new CoordinateSystem();
        system.SetOrigin(new Point2D(10, 5));

        Point3D local = system.ToLocal(new Point2D(13, 9));

        local.X.Should().Be(3);
        local.Y.Should().Be(4);
        local.Z.Should().Be(0);
    }

    [Fact]
    public void SettingOrigin_RaisesChanged_OnlyWhenItMoves()
    {
        var system = new CoordinateSystem();
        int raised = 0;
        system.Changed += (_, _) => raised++;

        system.SetOrigin(new Point2D(1, 2));
        system.SetOrigin(new Point2D(1, 2)); // no change

        raised.Should().Be(1);
    }

    [Fact]
    public void Document_HasOrigin_AtZeroByDefault()
    {
        var document = new CadDocument();
        document.CoordinateSystem.Origin.Should().Be(Point3D.Origin);
    }

    [Fact]
    public void Point3D_ProjectsToPlane()
    {
        var point = new Point3D(2, 3, 9);
        point.ToPoint2D().Should().Be(new Point2D(2, 3));
    }
}
