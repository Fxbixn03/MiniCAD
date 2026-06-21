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

public class ConstructionTests
{
    private sealed class ConstructionContext : IToolContext
    {
        public ConstructionContext(CadDocument document) => Document = document;
        public ICadDocument Document { get; }
        public Selection Selection { get; } = new();
        public double PickTolerance => 0.5;
        public StrokeStyle? DefaultStrokeOverride => null;
        public bool ConstructionMode => true; // override the default
        public UndoRedoManager Commands { get; } = new();
        public void Execute(IUndoableCommand command) => Commands.Execute(command);
        public void Push(IUndoableCommand command) => Commands.Push(command);
        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);
        public void RequestRedraw() { }
        public void ReturnToDefaultTool() { }
    }

    [Fact]
    public void Entity_IsConstruction_DefaultsFalse_AndClones()
    {
        var line = new LineEntity(Point2D.Origin, new Point2D(10, 0));
        line.IsConstruction.Should().BeFalse();

        line.IsConstruction = true;
        ((LineEntity)line.Clone()).IsConstruction.Should().BeTrue();
    }

    [Fact]
    public void DrawingToolInConstructionMode_FlagsCreatedGeometry()
    {
        var doc = new CadDocument();
        var ctx = new ConstructionContext(doc);
        var tool = new LineTool();
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(10, 10), Button = ToolButton.Left });

        var line = doc.Entities.OfType<LineEntity>().Should().ContainSingle().Subject;
        line.IsConstruction.Should().BeTrue();
    }

    [Fact]
    public void IsConstruction_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new LineEntity(Point2D.Origin, new Point2D(5, 5)) { IsConstruction = true });
        doc.AddEntity(new CircleEntity(new Point2D(2, 2), 3)); // normal

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.Entities.OfType<LineEntity>().Single().IsConstruction.Should().BeTrue();
        restored.Entities.OfType<CircleEntity>().Single().IsConstruction.Should().BeFalse();
    }
}
