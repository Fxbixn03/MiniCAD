using System.Collections.Generic;
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

public class LeaderTests
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

    [Fact]
    public void LeaderEntity_SnapsToVerticesAndHitTestsTheLine()
    {
        var leader = new LeaderEntity(new[] { new Point2D(0, 0), new Point2D(10, 10) }, "Hinweis", 5);

        leader.SnapPoints.Should().Contain(new Point2D(0, 0)).And.Contain(new Point2D(10, 10));
        leader.HitTest(new Point2D(5, 5), 0.5).Should().BeTrue();    // on the line
        leader.HitTest(new Point2D(0, 10), 0.5).Should().BeFalse();  // off the line and label
    }

    [Fact]
    public void LeaderEntity_RequiresAtLeastTwoPoints()
    {
        var act = () => new LeaderEntity(new[] { new Point2D(0, 0) }, "x");
        act.Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void LeaderEntity_Transform_MovesPointsAndScalesSizes()
    {
        var leader = new LeaderEntity(new[] { new Point2D(0, 0), new Point2D(2, 0) }, "x", 10, 8);

        leader.Transform(Matrix2D.Scaling(2, 2, Point2D.Origin));

        leader.Points[1].Should().Be(new Point2D(4, 0));
        leader.TextHeight.Should().BeApproximately(20, 1e-9);
        leader.ArrowSize.Should().BeApproximately(16, 1e-9);
    }

    [Fact]
    public void LeaderEntity_GripMovesIndexedVertex()
    {
        var leader = new LeaderEntity(new[] { new Point2D(0, 0), new Point2D(5, 0) }, "x");
        Grip tip = leader.GetGrips()[0];

        leader.MoveGrip(tip, new Point2D(-1, -1));

        leader.Points[0].Should().Be(new Point2D(-1, -1));
    }

    [Fact]
    public void LeaderEntity_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new LeaderEntity(new[] { new Point2D(0, 0), new Point2D(8, 4), new Point2D(14, 4) }, "Callout", 6, 5));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var leader = restored.Entities.OfType<LeaderEntity>().Should().ContainSingle().Subject;
        leader.Points.Should().HaveCount(3);
        leader.Points[0].Should().Be(new Point2D(0, 0));
        leader.Points[2].Should().Be(new Point2D(14, 4));
        leader.Text.Should().Be("Callout");
        leader.TextHeight.Should().Be(6);
        leader.ArrowSize.Should().Be(5);
    }

    [Fact]
    public void LeaderTool_BuildsLeaderAfterLabelIsTyped()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LeaderTool { TextHeight = 8 };
        tool.Activate(ctx);

        TextEditRequest? request = null;
        tool.EditRequested += r => request = r;

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(10, 6), Button = ToolButton.Left });
        tool.KeyDown(ToolKey.Enter); // finish the line → asks for the label

        request.Should().NotBeNull();
        doc.Entities.Should().BeEmpty(); // nothing yet

        request!.Commit("Achtung");

        var leader = doc.Entities.OfType<LeaderEntity>().Should().ContainSingle().Subject;
        leader.Points.Should().HaveCount(2);
        leader.Text.Should().Be("Achtung");
        leader.TextHeight.Should().Be(8);
    }

    [Fact]
    public void LeaderTool_CancelLabel_DiscardsTheLeader()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LeaderTool();
        tool.Activate(ctx);

        TextEditRequest? request = null;
        tool.EditRequested += r => request = r;

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 5), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Right }); // finish

        request!.Cancel();

        doc.Entities.Should().BeEmpty();
    }

    [Fact]
    public void LeaderTool_SinglePoint_DoesNotRequestText()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LeaderTool();
        tool.Activate(ctx);

        bool requested = false;
        tool.EditRequested += _ => requested = true;

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.KeyDown(ToolKey.Enter); // only one point → nothing to finish

        requested.Should().BeFalse();
        doc.Entities.Should().BeEmpty();
    }
}
