using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class TrimExtendTests
{
    private const double Tol = 1e-6;

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

    private static ToolPointerInput Click(double x, double y, ToolModifiers modifiers = ToolModifiers.None)
        => new() { World = new Point2D(x, y), Button = ToolButton.Left, Modifiers = modifiers };

    // ----- Intersection backbone -----

    [Fact]
    public void EntityIntersection_LineLine_FindsCrossing()
    {
        var points = EntityIntersection.Between(
            new LineEntity(new Point2D(0, 0), new Point2D(10, 0)),
            new LineEntity(new Point2D(5, -5), new Point2D(5, 5)));

        points.Should().ContainSingle();
        points[0].X.Should().BeApproximately(5, Tol);
        points[0].Y.Should().BeApproximately(0, Tol);
    }

    [Fact]
    public void EntityIntersection_LineCircle_FindsBothCrossings()
    {
        var points = EntityIntersection.Between(
            new LineEntity(new Point2D(-10, 0), new Point2D(10, 0)),
            new CircleEntity(new Point2D(0, 0), 5));

        points.Should().HaveCount(2);
        points.Select(p => p.X).Should().Contain(x => Math.Abs(x - 5) < Tol)
            .And.Contain(x => Math.Abs(x + 5) < Tol);
    }

    // ----- Trim geometry -----

    [Fact]
    public void Trim_LineBetweenTwoCuts_SplitsIntoTwoPieces()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var cuts = new[] { new Point2D(3, 0), new Point2D(7, 0) };

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(line, new Point2D(5, 0), cuts);

        pieces.Should().HaveCount(2);
        var left = pieces![0].Should().BeOfType<LineEntity>().Subject;
        var right = pieces[1].Should().BeOfType<LineEntity>().Subject;
        left.Start.X.Should().BeApproximately(0, Tol);
        left.End.X.Should().BeApproximately(3, Tol);
        right.Start.X.Should().BeApproximately(7, Tol);
        right.End.X.Should().BeApproximately(10, Tol);
    }

    [Fact]
    public void Trim_LineDanglingEnd_ShortensToCut()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var cuts = new[] { new Point2D(3, 0) };

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(line, new Point2D(8, 0), cuts);

        pieces.Should().ContainSingle();
        var kept = pieces![0].Should().BeOfType<LineEntity>().Subject;
        kept.Start.X.Should().BeApproximately(0, Tol);
        kept.End.X.Should().BeApproximately(3, Tol);
    }

    [Fact]
    public void Trim_CircleAtTwoCuts_BecomesComplementaryArc()
    {
        var circle = new CircleEntity(new Point2D(0, 0), 5);
        var cuts = new[] { new Point2D(5, 0), new Point2D(0, 5) }; // angles 0 and 90°

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(circle, new Point2D(3.5, 3.5), cuts); // pick ~45°

        var arc = pieces.Should().ContainSingle().Which.Should().BeOfType<ArcEntity>().Subject;
        arc.StartAngle.Should().BeApproximately(Math.PI / 2, Tol);          // kept starts at 90°
        arc.SweepAngle.Should().BeApproximately(3 * Math.PI / 2, Tol);      // sweeps the long way round
    }

    [Fact]
    public void Trim_Arc_RemovesClickedPortion()
    {
        // Upper semicircle 0°..180°, cut at 90°.
        var arc = new ArcEntity(new Point2D(0, 0), 5, 0, Math.PI);
        var cuts = new[] { new Point2D(0, 5) };

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(arc, new Point2D(3.5, 3.5), cuts); // pick ~45°

        var kept = pieces.Should().ContainSingle().Which.Should().BeOfType<ArcEntity>().Subject;
        kept.StartAngle.Should().BeApproximately(Math.PI / 2, Tol);
        kept.SweepAngle.Should().BeApproximately(Math.PI / 2, Tol);
    }

    [Fact]
    public void Trim_OpenPolyline_KeepsRemainderThroughVertex()
    {
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(5, 0), new Point2D(5, 5) });
        var cuts = new[] { new Point2D(3, 0) };

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(poly, new Point2D(1, 0), cuts); // pick before the cut

        var kept = pieces.Should().ContainSingle().Which.Should().BeOfType<PolylineEntity>().Subject;
        kept.IsClosed.Should().BeFalse();
        kept.Points.Should().HaveCount(3); // (3,0) -> (5,0) -> (5,5)
        kept.Points[0].X.Should().BeApproximately(3, Tol);
    }

    [Fact]
    public void Trim_ClosedPolyline_OpensIntoSinglePolyline()
    {
        var rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(10, 10));
        var cuts = new[] { new Point2D(5, 0), new Point2D(10, 5) }; // bottom + right edges

        IReadOnlyList<IEntity>? pieces = TrimGeometry.Trim(rect, new Point2D(8, 0), cuts); // pick the bottom-right span

        var kept = pieces.Should().ContainSingle().Which.Should().BeOfType<PolylineEntity>().Subject;
        kept.IsClosed.Should().BeFalse();
        kept.Points.Should().HaveCount(5);
    }

    [Fact]
    public void Extend_Line_ReachesCuttingLine()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(5, 0));
        var cutter = new LineEntity(new Point2D(10, -1), new Point2D(10, 1));

        IEntity? extended = TrimGeometry.Extend(line, new Point2D(5, 0), new IEntity[] { cutter });

        var result = extended.Should().BeOfType<LineEntity>().Subject;
        result.Start.Should().Be(new Point2D(0, 0));
        result.End.X.Should().BeApproximately(10, Tol);
        result.End.Y.Should().BeApproximately(0, Tol);
    }

    // ----- Tool -----

    [Fact]
    public void TrimTool_TrimsPickedSpan_AsOneUndoableStep()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var target = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var cutter = new LineEntity(new Point2D(5, -5), new Point2D(5, 5));
        doc.AddEntity(target);
        doc.AddEntity(cutter);
        var tool = new TrimExtendTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(8, 0)); // pick the right part of the target

        doc.Entities.Should().HaveCount(2);                  // cutter + trimmed piece
        doc.Entities.Should().NotContain(target);
        var kept = doc.Entities.OfType<LineEntity>().Single(l => l != cutter);
        kept.End.X.Should().BeApproximately(5, Tol);

        ctx.Commands.Undo();
        doc.Entities.Should().Contain(target);               // restored in one step
    }

    [Fact]
    public void TrimTool_ExtendWithShift_GrowsToCuttingLine()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var target = new LineEntity(new Point2D(0, 0), new Point2D(5, 0));
        var cutter = new LineEntity(new Point2D(10, -5), new Point2D(10, 5));
        doc.AddEntity(target);
        doc.AddEntity(cutter);
        var tool = new TrimExtendTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(5, 0, ToolModifiers.Shift)); // shift = extend the near end

        var extended = doc.Entities.OfType<LineEntity>().Single(l => l != cutter);
        extended.End.X.Should().BeApproximately(10, Tol);
    }
}
