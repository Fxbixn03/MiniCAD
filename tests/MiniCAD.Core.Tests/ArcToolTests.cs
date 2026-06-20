using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class ArcToolTests
{
    private const double Tol = 1e-9;

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

    private static ToolPointerInput Click(double x, double y)
        => new() { World = new Point2D(x, y), Button = ToolButton.Left };

    [Fact]
    public void FromThreePoints_UpperSemicircle()
    {
        ArcEntity arc = ArcEntity.FromThreePoints(new Point2D(5, 0), new Point2D(0, 5), new Point2D(-5, 0))!;

        arc.Center.X.Should().BeApproximately(0, Tol);
        arc.Center.Y.Should().BeApproximately(0, Tol);
        arc.Radius.Should().BeApproximately(5, Tol);
        arc.StartAngle.Should().BeApproximately(0, Tol);
        arc.SweepAngle.Should().BeApproximately(Math.PI, Tol); // CCW through the top
    }

    [Fact]
    public void FromThreePoints_ThroughBottom_SweepsClockwise()
    {
        ArcEntity arc = ArcEntity.FromThreePoints(new Point2D(5, 0), new Point2D(0, -5), new Point2D(-5, 0))!;

        arc.SweepAngle.Should().BeApproximately(-Math.PI, Tol); // CW through the bottom
    }

    [Fact]
    public void FromThreePoints_Collinear_ReturnsNull()
        => ArcEntity.FromThreePoints(new Point2D(0, 0), new Point2D(5, 0), new Point2D(10, 0)).Should().BeNull();

    [Fact]
    public void FromCenterStartEnd_QuarterArc()
    {
        ArcEntity arc = ArcEntity.FromCenterStartEnd(new Point2D(0, 0), new Point2D(5, 0), new Point2D(0, 5))!;

        arc.Radius.Should().BeApproximately(5, Tol);
        arc.StartAngle.Should().BeApproximately(0, Tol);
        arc.SweepAngle.Should().BeApproximately(Math.PI / 2, Tol);
    }

    [Fact]
    public void ArcTool_ThreeClicks_AddsArc()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new ArcTool { Mode = ArcMode.ThreePoint };
        tool.Activate(ctx);

        tool.PointerDown(Click(5, 0));
        tool.PointerDown(Click(0, 5));
        tool.PointerDown(Click(-5, 0));

        var arc = doc.Entities.Should().ContainSingle().Which.Should().BeOfType<ArcEntity>().Subject;
        arc.Radius.Should().BeApproximately(5, Tol);
    }

    [Fact]
    public void ArcTool_PreviewsArc_AfterTwoClicks()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new ArcTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(5, 0));
        tool.PointerDown(Click(0, 5));
        tool.PointerMove(new ToolPointerInput { World = new Point2D(-5, 0), Button = ToolButton.None });

        tool.GetOverlay().Should().Contain(o => o.Entity is ArcEntity);
    }
}
