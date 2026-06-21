using System;
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

public class DimensionTypesTests
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

    // ----- #27 Angular -----

    [Fact]
    public void AngularDimension_MeasuresTheAngleContainingTheArcPoint()
    {
        // Rays along +X and +Y from the origin → 90°. Arc point in the first quadrant.
        var dim = new AngularDimensionEntity(Point2D.Origin, new Point2D(10, 0), new Point2D(0, 10), new Point2D(7, 7));

        dim.MeasuredValue.Should().BeApproximately(90, 1e-6);
        dim.DisplayText.Should().Be("90°");
    }

    [Fact]
    public void AngularDimension_ArcPointOnOtherSide_MeasuresReflexComplement()
    {
        // Same rays but the arc point in the third quadrant selects the 270° side.
        var dim = new AngularDimensionEntity(Point2D.Origin, new Point2D(10, 0), new Point2D(0, 10), new Point2D(-7, -7));

        dim.MeasuredValue.Should().BeApproximately(270, 1e-6);
    }

    [Fact]
    public void AngularDimension_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new AngularDimensionEntity(new Point2D(1, 1), new Point2D(11, 1), new Point2D(1, 11), new Point2D(8, 8)));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var dim = restored.Entities.OfType<AngularDimensionEntity>().Should().ContainSingle().Subject;
        dim.Vertex.Should().Be(new Point2D(1, 1));
        dim.ArcPoint.Should().Be(new Point2D(8, 8));
    }

    // ----- #28 Radial / diameter -----

    [Fact]
    public void RadialDimension_ReportsRadiusOrDiameter_WithPrefix()
    {
        var radius = new RadialDimensionEntity(Point2D.Origin, new Point2D(20, 0), new Point2D(30, 10));
        var diameter = new RadialDimensionEntity(Point2D.Origin, new Point2D(20, 0), new Point2D(30, 10), isDiameter: true);

        radius.MeasuredValue.Should().BeApproximately(20, 1e-9);
        radius.DisplayText.Should().Be("R20");
        diameter.MeasuredValue.Should().BeApproximately(40, 1e-9);
        diameter.DisplayText.Should().Be("⌀40");
    }

    [Fact]
    public void RadialDimensionTool_PicksCircle_ThenPlacesText()
    {
        var doc = new CadDocument();
        doc.AddEntity(new CircleEntity(new Point2D(0, 0), 25));
        var ctx = new TestContext(doc);
        var tool = new RadialDimensionTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(25, 0)); // click on the circle's edge
        doc.Entities.OfType<RadialDimensionEntity>().Should().BeEmpty();
        tool.PointerDown(Click(40, 10)); // text position

        var dim = doc.Entities.OfType<RadialDimensionEntity>().Should().ContainSingle().Subject;
        dim.Center.Should().Be(new Point2D(0, 0));
        dim.MeasuredValue.Should().BeApproximately(25, 1e-6);
    }

    [Fact]
    public void RadialDimension_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new RadialDimensionEntity(new Point2D(2, 3), new Point2D(12, 3), new Point2D(20, 8), isDiameter: true));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var dim = restored.Entities.OfType<RadialDimensionEntity>().Should().ContainSingle().Subject;
        dim.IsDiameter.Should().BeTrue();
        dim.Center.Should().Be(new Point2D(2, 3));
        dim.EdgePoint.Should().Be(new Point2D(12, 3));
    }

    // ----- #29 Chain / baseline -----

    [Fact]
    public void ChainMode_ContinuesFromPreviousSecondPoint()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LinearDimensionTool { Kind = LinearDimensionKind.Horizontal, ContinueMode = DimensionContinueMode.Chain };
        tool.Activate(ctx);

        // First dimension: 0..10, dimension line at y=20.
        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(10, 0));
        tool.PointerDown(Click(0, 20));
        // Continued single clicks chain on.
        tool.PointerDown(Click(25, 0));
        tool.PointerDown(Click(40, 0));

        var dims = doc.Entities.OfType<LinearDimensionEntity>().ToList();
        dims.Should().HaveCount(3);
        dims[1].P1.Should().Be(new Point2D(10, 0)); // starts where #1 ended
        dims[2].P1.Should().Be(new Point2D(25, 0));
        dims[1].MeasuredValue.Should().BeApproximately(15, 1e-9);
    }

    [Fact]
    public void BaselineMode_AllShareTheFirstPoint_AndStagger()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LinearDimensionTool { Kind = LinearDimensionKind.Horizontal, ContinueMode = DimensionContinueMode.Baseline };
        tool.Activate(ctx);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(10, 0));
        tool.PointerDown(Click(0, 20));
        tool.PointerDown(Click(25, 0));
        tool.PointerDown(Click(40, 0));

        var dims = doc.Entities.OfType<LinearDimensionEntity>().ToList();
        dims.Should().HaveCount(3);
        dims.Select(d => d.P1).Should().AllBeEquivalentTo(new Point2D(0, 0)); // common baseline origin
        dims[2].MeasuredValue.Should().BeApproximately(40, 1e-9);
        // Baseline dimension lines stagger outward (different offsets).
        dims[1].DimLinePoint.Y.Should().NotBe(dims[2].DimLinePoint.Y);
    }
}
