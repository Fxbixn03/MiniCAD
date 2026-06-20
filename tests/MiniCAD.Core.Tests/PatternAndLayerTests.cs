using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class PatternAndLayerTests
{
    [Fact]
    public void Document_FindPattern_ResolvesProjectThenGlobal()
    {
        var document = new CadDocument();
        var custom = new HatchPattern("Eigen", new[] { new HatchLineDefinition(30, 12) }, Color.White);
        document.AddPattern(custom);

        document.FindPattern(custom.Id).Should().BeSameAs(custom);
        document.FindPattern(PatternLibrary.Masonry.Id).Should().BeSameAs(PatternLibrary.Masonry);
        document.FindPattern(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void RemovingPattern_ClearsFillsThatUsedIt()
    {
        var document = new CadDocument();
        var pattern = new HatchPattern("p", new[] { new HatchLineDefinition(0, 5) }, Color.White);
        document.AddPattern(pattern);

        var poly = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 4));
        poly.Fill = pattern;
        document.AddEntity(poly);

        document.RemovePattern(pattern);

        poly.Fill.Should().BeNull();
    }

    [Fact]
    public void AddStandardLayers_AddsAll_AndSkipsDuplicatesOnSecondRun()
    {
        var document = new CadDocument();
        int firstRun = document.AddStandardLayers();

        firstRun.Should().Be(StandardLayers.All.Count);
        document.AddStandardLayers().Should().Be(0); // already present
        document.Layers.Select(l => l.Name).Should().Contain("Wände tragend");
    }

    [Fact]
    public void RoundTrip_PreservesFill_AndOrigin()
    {
        var document = new CadDocument();
        document.CoordinateSystem.SetOrigin(new Point2D(12, -8));

        var custom = new HatchPattern("Projektmuster", new[] { new HatchLineDefinition(60, 9) }, Color.Red, 2.0);
        document.AddPattern(custom);

        var wall = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(5, 5));
        wall.Fill = PatternLibrary.Masonry;
        document.AddEntity(wall);

        var filledCustom = PolylineEntity.Rectangle(new Point2D(6, 0), new Point2D(9, 5));
        filledCustom.Fill = custom;
        document.AddEntity(filledCustom);

        // Round-trip through the file format.
        var fileDto = new ProjectFileDto { Document = DocumentMapper.ToDto(document) };
        string json = ProjectSerializer.Serialize(fileDto);
        var restored = new CadDocument();
        DocumentMapper.Apply(ProjectSerializer.Deserialize(json).Document, restored);

        restored.CoordinateSystem.Origin.Should().Be(new Point3D(12, -8, 0));
        restored.Patterns.Should().ContainSingle(p => p.Name == "Projektmuster");

        var polylines = restored.Entities.OfType<PolylineEntity>().ToList();
        polylines.Should().Contain(p => p.Fill != null && p.Fill.Id == PatternLibrary.Masonry.Id);
        polylines.Should().Contain(p => p.Fill != null && p.Fill.Name == "Projektmuster");
    }
}
