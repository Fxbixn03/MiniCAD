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

public class SplineTests
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

    private static ToolPointerInput Click(double x, double y, int clickCount = 1)
        => new() { World = new Point2D(x, y), Button = ToolButton.Left, ClickCount = clickCount };

    [Fact]
    public void Spline_PassesThroughFitPoints()
    {
        var spline = new SplineEntity(new[] { new Point2D(0, 0), new Point2D(5, 5), new Point2D(10, 0) });

        spline.HitTest(new Point2D(5, 5), 0.01).Should().BeTrue();   // interpolates the fit point
        spline.HitTest(new Point2D(5, -5), 0.1).Should().BeFalse();  // clear of the curve
    }

    [Fact]
    public void Spline_WithTwoPoints_IsAStraightLine()
    {
        var spline = new SplineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0) });

        spline.HitTest(new Point2D(5, 0), 0.01).Should().BeTrue();
        spline.Points.Should().HaveCount(2);
    }

    [Fact]
    public void SplineTool_FinishesOnRightClick()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new SplineTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(5, 5));
        tool.PointerDown(Click(10, 0));
        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Right });

        var spline = doc.Entities.Should().ContainSingle().Which.Should().BeOfType<SplineEntity>().Subject;
        spline.Points.Should().HaveCount(3);
    }

    [Fact]
    public void Spline_GripMovesFitPoint()
    {
        var spline = new SplineEntity(new[] { new Point2D(0, 0), new Point2D(5, 5), new Point2D(10, 0) });
        Grip middle = spline.GetGrips()[1];

        spline.MoveGrip(middle, new Point2D(5, 9));

        spline.Points[1].Should().Be(new Point2D(5, 9));
    }

    [Fact]
    public void Spline_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new SplineEntity(new[] { new Point2D(0, 0), new Point2D(5, 5), new Point2D(10, 0) }));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var spline = restored.Entities.OfType<SplineEntity>().Should().ContainSingle().Subject;
        spline.Points.Should().HaveCount(3);
        spline.Points[1].Should().Be(new Point2D(5, 5));
    }
}
