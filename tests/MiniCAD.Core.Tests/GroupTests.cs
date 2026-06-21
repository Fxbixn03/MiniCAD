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

public class GroupTests
{
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;
        public ICadDocument Document { get; }
        public Selection Selection { get; } = new();
        public double PickTolerance { get; set; } = 0.5;
        public StrokeStyle? DefaultStrokeOverride { get; set; }
        public void Execute(IUndoableCommand command) => command.Execute();
        public void Push(IUndoableCommand command) { }
        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);
        public void RequestRedraw() { }
        public void ReturnToDefaultTool() { }
    }

    [Fact]
    public void ClickingAGroupedEntity_SelectsTheWholeGroup()
    {
        var doc = new CadDocument();
        var groupId = Guid.NewGuid();
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0)) { GroupId = groupId };
        var b = new LineEntity(new Point2D(0, 5), new Point2D(10, 5)) { GroupId = groupId };
        var loose = new LineEntity(new Point2D(0, 100), new Point2D(10, 100));
        doc.AddEntity(a);
        doc.AddEntity(b);
        doc.AddEntity(loose);

        var ctx = new TestContext(doc);
        var tool = new SelectTool();
        tool.Activate(ctx);

        // Click on entity a → the whole group (a + b) becomes selected.
        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 0), Button = ToolButton.Left });

        ctx.Selection.Items.Should().Contain(a).And.Contain(b);
        ctx.Selection.Items.Should().NotContain(loose);
    }

    [Fact]
    public void GroupId_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        var groupId = Guid.NewGuid();
        doc.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)) { GroupId = groupId });
        doc.AddEntity(new CircleEntity(new Point2D(2, 2), 3) { GroupId = groupId });

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.Entities.Select(e => e.GroupId).Distinct().Should().ContainSingle()
            .Which.Should().Be(groupId);
    }
}
