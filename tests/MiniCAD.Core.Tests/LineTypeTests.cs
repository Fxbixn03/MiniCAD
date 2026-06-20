using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class LineTypeTests
{
    [Fact]
    public void StrokeStyle_CarriesLineType_AndEqualityRespectsIt()
    {
        var solid = new StrokeStyle(Color.White, 1.0);
        var dashed = solid.WithLineType(LineType.Dashed);

        solid.LineType.Should().Be(LineType.Solid);
        dashed.LineType.Should().Be(LineType.Dashed);
        dashed.Color.Should().Be(solid.Color);
        dashed.Width.Should().Be(solid.Width);
        (dashed == solid).Should().BeFalse();
        (dashed == solid.WithLineType(LineType.Dashed)).Should().BeTrue();
    }

    [Fact]
    public void StrokeStyle_CarriesLineWeightMm_AndEqualityRespectsIt()
    {
        var thin = new StrokeStyle(Color.White, 1.0);
        var plotted = thin.WithLineWeight(0.35);

        thin.LineWeightMm.Should().Be(0.0);
        plotted.LineWeightMm.Should().Be(0.35);
        (plotted == thin).Should().BeFalse();
        new StrokeStyle(Color.White, 1.0, LineType.Solid, -5).LineWeightMm.Should().Be(0.0); // clamped
    }

    [Fact]
    public void LayerLineWeight_PersistsRoundTrip()
    {
        var doc = new CadDocument();
        doc.SetLayerLineWeight(doc.DefaultLayer, 0.5);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.DefaultLayer.Stroke.LineWeightMm.Should().Be(0.5);
    }

    [Fact]
    public void LayerLineType_PersistsAndAppliesToEntities()
    {
        var doc = new CadDocument();
        doc.SetLayerLineType(doc.DefaultLayer, LineType.Center);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.DefaultLayer.Stroke.LineType.Should().Be(LineType.Center);
    }

    [Fact]
    public void EntityStrokeOverrideLineType_SurvivesRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new LineEntity(Point2D.Origin, new Point2D(10, 0))
        {
            StrokeOverride = new StrokeStyle(Color.Red, 2.0, LineType.DashDot),
        });

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var line = restored.Entities.OfType<LineEntity>().Should().ContainSingle().Subject;
        line.StrokeOverride.Should().NotBeNull();
        line.StrokeOverride!.Value.LineType.Should().Be(LineType.DashDot);
        line.StrokeOverride!.Value.Width.Should().Be(2.0);
    }
}
