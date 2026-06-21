using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class BlockTests
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

    private sealed class CountingSurface : IRenderSurface
    {
        public int Lines { get; private set; }
        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) => Lines++;
        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke) { }
        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke) { }
        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign h, TextVAlign v, string? fontFamily, double widthFactor, in StrokeStyle stroke) { }
    }

    private static BlockDefinition MakeBlock()
    {
        // A 10×0 line and a 0×10 line meeting at the origin; base point at origin.
        var entities = new IEntity[]
        {
            new LineEntity(new Point2D(0, 0), new Point2D(10, 0)),
            new LineEntity(new Point2D(0, 0), new Point2D(0, 10)),
        };
        return new BlockDefinition("Winkel", Point2D.Origin, entities);
    }

    [Fact]
    public void BlockReference_TransformsGeometryToInsertionPoint()
    {
        BlockDefinition def = MakeBlock();
        var reference = new BlockReferenceEntity(def, new Point2D(100, 50));

        // The block's far endpoint (10,0) lands at (110,50).
        reference.Bounds.MinX.Should().BeApproximately(100, 1e-6);
        reference.Bounds.MaxX.Should().BeApproximately(110, 1e-6);
        reference.HitTest(new Point2D(105, 50), 0.5).Should().BeTrue();
        reference.SnapPoints.Should().Contain(new Point2D(100, 50));
    }

    [Fact]
    public void BlockReference_ScaleAndRotation_Apply()
    {
        BlockDefinition def = MakeBlock();
        var reference = new BlockReferenceEntity(def, Point2D.Origin, scale: 2.0, rotation: 0.0);

        // Scaled: the (10,0) endpoint becomes (20,0).
        reference.HitTest(new Point2D(20, 0), 0.5).Should().BeTrue();
        reference.Bounds.MaxX.Should().BeApproximately(20, 1e-6);
    }

    [Fact]
    public void BlockReference_Render_DrawsEachChild()
    {
        var reference = new BlockReferenceEntity(MakeBlock(), Point2D.Origin);
        var surface = new CountingSurface();

        reference.Render(surface, StrokeStyle.Default);

        surface.Lines.Should().Be(2); // the two block lines
    }

    [Fact]
    public void BlockReference_NullDefinition_RendersNothing()
    {
        var reference = new BlockReferenceEntity(System.Guid.NewGuid(), Point2D.Origin);
        var surface = new CountingSurface();

        reference.Render(surface, StrokeStyle.Default);

        surface.Lines.Should().Be(0);
        reference.HitTest(Point2D.Origin, 0.5).Should().BeFalse();
    }

    [Fact]
    public void BlockInsertTool_PlacesReference()
    {
        var doc = new CadDocument();
        BlockDefinition def = doc.AddBlockDefinition(MakeBlock());
        var ctx = new TestContext(doc);
        var tool = new BlockInsertTool { Definition = def };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(7, 8), Button = ToolButton.Left });

        var reference = doc.Entities.OfType<BlockReferenceEntity>().Should().ContainSingle().Subject;
        reference.Position.Should().Be(new Point2D(7, 8));
        reference.DefinitionId.Should().Be(def.Id);
    }

    [Fact]
    public void Block_SurvivesPersistenceRoundTrip_AndRelinks()
    {
        var doc = new CadDocument();
        BlockDefinition def = doc.AddBlockDefinition(MakeBlock());
        var reference = new BlockReferenceEntity(def, new Point2D(5, 5), 2.0, 0.5);
        reference.Attributes["Raumname"] = "Büro";
        doc.AddEntity(reference);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.BlockDefinitions.Should().ContainSingle(d => d.Name == "Winkel");
        var loaded = restored.Entities.OfType<BlockReferenceEntity>().Should().ContainSingle().Subject;
        loaded.Definition.Should().NotBeNull(); // relinked
        loaded.Definition!.Id.Should().Be(def.Id);
        loaded.Scale.Should().Be(2.0);
        loaded.Rotation.Should().Be(0.5);
        loaded.Attributes["Raumname"].Should().Be("Büro");
    }
}
