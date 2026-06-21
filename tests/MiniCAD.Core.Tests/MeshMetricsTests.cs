using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class MeshMetricsTests
{
    [Fact]
    public void Box_VolumeSurfaceCentroid_MatchAnalytic()
    {
        MeshMetrics m = MeshMetrics.Compute(Mesh3D.Box(1000, 2000, 3000));

        m.Volume.Should().BeApproximately(1000.0 * 2000 * 3000, 1.0);          // w·h·d
        m.SurfaceArea.Should().BeApproximately(2.0 * (1000 * 2000 + 1000 * 3000 + 2000 * 3000), 1.0);
        m.Centroid.X.Should().BeApproximately(0, 1e-6);
        m.Centroid.Y.Should().BeApproximately(0, 1e-6);
        m.Centroid.Z.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void Cylinder_VolumeApproximatesPiRSquaredH()
    {
        MeshMetrics m = MeshMetrics.Compute(Mesh3D.Cylinder(500, 1000, 48));
        double analytic = System.Math.PI * 500 * 500 * 1000;
        // A 48-gon prism slightly under-estimates the circle; within ~1%.
        m.Volume.Should().BeApproximately(analytic, analytic * 0.01);
    }

    [Fact]
    public void MassTakeoff_SumsVolumeAndMassByMaterial()
    {
        var doc = new CadDocument();
        doc.AddEntity(new WallEntity(new Point2D(0, 0), new Point2D(5000, 0), 240, 2500, 0)); // 3 m³
        foreach (Model3DObject model in ArchModelBuilder.Build(doc.Entities))
            doc.AddModelObject(model);

        var rows = MassTakeoff.Compute(doc.Models);

        MassTakeoffRow wall = rows.Single(r => r.Material == "Kalksandstein");
        wall.Count.Should().Be(1);
        wall.VolumeM3.Should().BeApproximately(3.0, 1e-6);     // 5000·240·2500 mm³ = 3 m³
        wall.MassKg.Should().BeApproximately(3.0 * 2000, 1e-3); // Kalksandstein 2000 kg/m³

        MassTakeoff.ToCsv(rows).Should().Contain("Kalksandstein");
    }
}
