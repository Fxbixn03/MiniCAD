using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class FilletTests
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

    private static (LineEntity Horizontal, LineEntity Vertical) RightAngle()
        => (new LineEntity(new Point2D(0, 0), new Point2D(10, 0)),
            new LineEntity(new Point2D(0, 0), new Point2D(0, 10)));

    [Fact]
    public void Fillet_RightAngle_TrimsBothLinesAndBuildsTangentArc()
    {
        var (h, v) = RightAngle();

        FilletGeometry.Result result = FilletGeometry
            .Compute(h, new Point2D(5, 0), v, new Point2D(0, 5), distance: 2, chamfer: false)!.Value;

        result.Keep1.Should().Be(new Point2D(10, 0));
        result.Tangent1.X.Should().BeApproximately(2, Tol);
        result.Keep2.Should().Be(new Point2D(0, 10));
        result.Tangent2.Y.Should().BeApproximately(2, Tol);

        var arc = result.Connector.Should().BeOfType<ArcEntity>().Subject;
        arc.Center.X.Should().BeApproximately(2, Tol);
        arc.Center.Y.Should().BeApproximately(2, Tol);
        arc.Radius.Should().BeApproximately(2, Tol);
    }

    [Fact]
    public void Chamfer_RightAngle_ConnectsSetbacksWithLine()
    {
        var (h, v) = RightAngle();

        FilletGeometry.Result result = FilletGeometry
            .Compute(h, new Point2D(5, 0), v, new Point2D(0, 5), distance: 3, chamfer: true)!.Value;

        result.Tangent1.X.Should().BeApproximately(3, Tol);
        result.Tangent2.Y.Should().BeApproximately(3, Tol);
        var line = result.Connector.Should().BeOfType<LineEntity>().Subject;
        line.Start.X.Should().BeApproximately(3, Tol);
        line.End.Y.Should().BeApproximately(3, Tol);
    }

    [Fact]
    public void Fillet_ZeroDistance_MakesSharpCornerWithNoConnector()
    {
        var (h, v) = RightAngle();

        FilletGeometry.Result result = FilletGeometry
            .Compute(h, new Point2D(5, 0), v, new Point2D(0, 5), distance: 0, chamfer: false)!.Value;

        result.Tangent1.Should().Be(new Point2D(0, 0)); // both meet at the corner
        result.Tangent2.Should().Be(new Point2D(0, 0));
        result.Connector.Should().BeNull();
    }

    [Fact]
    public void Fillet_ParallelLines_ReturnsNull()
    {
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(0, 5), new Point2D(10, 5));

        FilletGeometry.Compute(a, new Point2D(5, 0), b, new Point2D(5, 5), distance: 1, chamfer: false)
            .Should().BeNull();
    }

    [Fact]
    public void FilletTool_TwoClicks_TrimsLinesAndAddsArc_Undoable()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var (h, v) = RightAngle();
        doc.AddEntity(h);
        doc.AddEntity(v);
        var tool = new FilletTool { Distance = 2 };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 0), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 5), Button = ToolButton.Left });

        doc.Entities.Should().HaveCount(3);                  // two trimmed lines + the fillet arc
        doc.Entities.OfType<ArcEntity>().Should().ContainSingle();
        h.End.X.Should().BeApproximately(2, Tol);            // horizontal trimmed back to the tangent

        ctx.Commands.Undo();
        doc.Entities.Should().HaveCount(2);                  // restored in one step
        h.End.Should().Be(new Point2D(10, 0));
    }
}
