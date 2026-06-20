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

public class PointEntityTests
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

        public void RequestRedraw()
        {
        }

        public void ReturnToDefaultTool()
        {
        }
    }

    [Fact]
    public void PointEntity_SnapsToAndHitTestsItsPosition()
    {
        var point = new PointEntity(new Point2D(3, 4), 6, PointStyle.Plus);

        point.SnapPoints.Should().ContainSingle().Which.Should().Be(new Point2D(3, 4));
        point.HitTest(new Point2D(3.2, 4.1), 0.5).Should().BeTrue();
        point.HitTest(new Point2D(10, 10), 0.5).Should().BeFalse();
        point.IntersectsRect(new Rect2D(0, 0, 5, 5)).Should().BeTrue();
        point.IntersectsRect(new Rect2D(10, 10, 20, 20)).Should().BeFalse();
    }

    [Fact]
    public void PointEntity_Transform_MovesPositionAndScalesSize()
    {
        var point = new PointEntity(new Point2D(1, 1), 4);

        point.Transform(Matrix2D.Scaling(2, 2, Point2D.Origin));

        point.Position.Should().Be(new Point2D(2, 2));
        point.Size.Should().BeApproximately(8, 1e-9);
    }

    [Fact]
    public void PointTool_Click_PlacesPoint()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new PointTool { Size = 8, Style = PointStyle.Square };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 6), Button = ToolButton.Left });

        var point = doc.Entities.Should().ContainSingle().Which.Should().BeOfType<PointEntity>().Subject;
        point.Position.Should().Be(new Point2D(5, 6));
        point.Size.Should().Be(8);
        point.Style.Should().Be(PointStyle.Square);
    }

    [Fact]
    public void PointEntity_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new PointEntity(new Point2D(3, 4), 7, PointStyle.Cross));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var point = restored.Entities.OfType<PointEntity>().Should().ContainSingle().Subject;
        point.Position.Should().Be(new Point2D(3, 4));
        point.Size.Should().Be(7);
        point.Style.Should().Be(PointStyle.Cross);
    }
}
