using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class ElevationOrdinateTests
{
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;
        public ICadDocument Document { get; }
        public Selection Selection { get; } = new();
        public double PickTolerance { get; set; } = 0.5;
        public StrokeStyle? DefaultStrokeOverride { get; set; }
        public UndoRedoManager Commands { get; } = new();
        public void Execute(IUndoableCommand command) => Commands.Execute(command);
        public void Push(IUndoableCommand command) => Commands.Push(command);
        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);
        public void RequestRedraw() { }
        public void ReturnToDefaultTool() { }
    }

    private static ToolPointerInput Click(double x, double y) =>
        new() { World = new Point2D(x, y), Button = ToolButton.Left };

    // ----- Höhenkote -----

    [Fact]
    public void Elevation_FormatsSignedValue_WithPlusMinusZero()
    {
        new ElevationDimensionEntity(Point2D.Origin, 0) { DecimalPlaces = 2 }.DisplayText.Should().Be("±0.00");
        new ElevationDimensionEntity(Point2D.Origin, 2.75) { DecimalPlaces = 2 }.DisplayText.Should().Be("+2.75");
        new ElevationDimensionEntity(Point2D.Origin, -1.2) { DecimalPlaces = 2 }.DisplayText.Should().Be("-1.20");
    }

    [Fact]
    public void ElevationTool_Click_PlacesMarkWithCurrentZ()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new ElevationDimensionTool { ZValue = 3.5 };
        tool.Activate(ctx);

        tool.PointerDown(Click(5, 5));

        var mark = doc.Entities.OfType<ElevationDimensionEntity>().Should().ContainSingle().Subject;
        mark.Position.Should().Be(new Point2D(5, 5));
        mark.ZValue.Should().Be(3.5);
        mark.DimStyleId.Should().Be(doc.ActiveDimStyle.Id);
    }

    [Fact]
    public void Elevation_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new ElevationDimensionEntity(new Point2D(1, 2), 4.25));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var mark = restored.Entities.OfType<ElevationDimensionEntity>().Should().ContainSingle().Subject;
        mark.Position.Should().Be(new Point2D(1, 2));
        mark.ZValue.Should().Be(4.25);
    }

    // ----- Koordinatenmaß / Ordinate -----

    [Fact]
    public void Ordinate_ShowsCoordinatesRelativeToOrigin()
    {
        var dim = new OrdinateDimensionEntity(new Point2D(30, 40), new Point2D(45, 45), new Point2D(10, 10));

        dim.DisplayText.Should().Be("X 20  Y 30");
    }

    [Fact]
    public void OrdinateTool_UsesDocumentNullpunkt()
    {
        var doc = new CadDocument();
        doc.CoordinateSystem.Origin = new Point3D(100, 200, 0);
        var ctx = new TestContext(doc);
        var tool = new OrdinateDimensionTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(150, 260)); // labelled point
        doc.Entities.OfType<OrdinateDimensionEntity>().Should().BeEmpty();
        tool.PointerDown(Click(180, 280)); // leader/text

        var dim = doc.Entities.OfType<OrdinateDimensionEntity>().Should().ContainSingle().Subject;
        dim.Origin.Should().Be(new Point2D(100, 200));
        dim.DisplayText.Should().Be("X 50  Y 60");
    }

    [Fact]
    public void Ordinate_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new OrdinateDimensionEntity(new Point2D(5, 6), new Point2D(15, 16), new Point2D(1, 1)));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var dim = restored.Entities.OfType<OrdinateDimensionEntity>().Should().ContainSingle().Subject;
        dim.Position.Should().Be(new Point2D(5, 6));
        dim.LeaderEnd.Should().Be(new Point2D(15, 16));
        dim.Origin.Should().Be(new Point2D(1, 1));
    }
}
