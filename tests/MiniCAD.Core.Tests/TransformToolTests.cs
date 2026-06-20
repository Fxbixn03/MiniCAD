using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class TransformToolTests
{
    /// <summary>Minimal in-memory context; snapping is a no-op so typed points stay exact.</summary>
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;

        public ICadDocument Document { get; }

        public Selection Selection { get; } = new();

        public double PickTolerance { get; set; } = 0.5;

        public StrokeStyle? DefaultStrokeOverride { get; set; }

        public int ReturnToDefaultCount { get; private set; }

        public UndoRedoManager Commands { get; } = new();

        public void Execute(IUndoableCommand command) => Commands.Execute(command);

        public void Push(IUndoableCommand command) => Commands.Push(command);

        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);

        public void RequestRedraw()
        {
        }

        public void ReturnToDefaultTool() => ReturnToDefaultCount++;
    }

    private static ToolPointerInput Click(double x, double y, ToolButton button = ToolButton.Left)
        => new() { World = new Point2D(x, y), Button = button };

    private static ToolPointerInput Move(double x, double y)
        => new() { World = new Point2D(x, y), Button = ToolButton.None };

    /// <summary>Sets up a context with <paramref name="entities"/> all selected and the tool active.</summary>
    private static TestContext Activate(SelectionTransformTool tool, CadDocument doc, params IEntity[] entities)
    {
        var ctx = new TestContext(doc);
        foreach (IEntity entity in entities)
        {
            doc.AddEntity(entity);
            ctx.Selection.Add(entity);
        }

        tool.Activate(ctx);
        return ctx;
    }

    [Fact]
    public void MoveTool_TranslatesSelection_AsSingleUndoStep()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new MoveTool();
        TestContext ctx = Activate(tool, doc, line);

        tool.PointerDown(Click(0, 0));   // base point
        tool.PointerDown(Click(3, 4));   // target point

        line.Start.Should().Be(new Point2D(3, 4));
        line.End.Should().Be(new Point2D(13, 4));

        ctx.Commands.CanUndo.Should().BeTrue();
        ctx.Commands.Undo();
        line.Start.Should().Be(new Point2D(0, 0)); // one step restores the original
    }

    [Fact]
    public void CopyTool_DuplicatesSelection_LeavingOriginalInPlace()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new MoveTool(copy: true);
        TestContext ctx = Activate(tool, doc, line);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(0, 5));

        doc.Entities.Should().HaveCount(2);
        line.Start.Should().Be(new Point2D(0, 0)); // original untouched
        var clone = doc.Entities[1].Should().BeOfType<LineEntity>().Subject;
        clone.Start.Should().Be(new Point2D(0, 5));
        clone.End.Should().Be(new Point2D(10, 5));

        ctx.Commands.Undo();
        doc.Entities.Should().ContainSingle().Which.Should().BeSameAs(line);
    }

    [Fact]
    public void RotateTool_RotatesSelectionAboutBasePoint()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new RotateTool();
        Activate(tool, doc, line);

        tool.PointerDown(Click(0, 0));   // pivot
        tool.PointerDown(Click(0, 5));   // direction straight up -> 90° CCW

        line.End.X.Should().BeApproximately(0, 1e-9);
        line.End.Y.Should().BeApproximately(10, 1e-9);
    }

    [Fact]
    public void MirrorTool_ReflectsSelectionAcrossAxis_AndIsUndoable()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new MirrorTool();
        TestContext ctx = Activate(tool, doc, line);

        // Mirror across the vertical line x = 5 -> x maps to 10 - x.
        tool.PointerDown(Click(5, 0));
        tool.PointerDown(Click(5, 10));

        line.Start.Should().Be(new Point2D(10, 0));
        line.End.Should().Be(new Point2D(0, 0));

        ctx.Commands.Undo();
        line.Start.Should().Be(new Point2D(0, 0)); // reflection is its own inverse
        line.End.Should().Be(new Point2D(10, 0));
    }

    [Fact]
    public void ScaleTool_ScalesSelectionByReferenceRatio()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new ScaleTool();
        Activate(tool, doc, line);

        tool.PointerDown(Click(0, 0));   // base
        tool.PointerDown(Click(1, 0));   // reference length = 1
        tool.PointerDown(Click(3, 0));   // target length = 3 -> factor 3

        line.End.X.Should().BeApproximately(30, 1e-9);
        line.End.Y.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void TransformTool_ShowsPreview_AfterBasePoint()
    {
        var doc = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var tool = new MoveTool();
        Activate(tool, doc, line);

        tool.GetOverlay().Should().BeEmpty();
        tool.PointerDown(Click(0, 0));
        tool.PointerMove(Move(5, 5));

        tool.GetOverlay().Should().NotBeEmpty(); // preview clone + rubber line
    }

    [Fact]
    public void TransformTool_WithEmptySelection_ReturnsToDefaultAndDoesNothing()
    {
        var doc = new CadDocument();
        var tool = new MoveTool();
        var ctx = new TestContext(doc);
        tool.Activate(ctx); // no selection captured

        tool.PointerDown(Click(0, 0));

        ctx.ReturnToDefaultCount.Should().Be(1);
        doc.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Matrix2D_Reflection_MirrorsPointsAcrossArbitraryAxis()
    {
        // Reflection across the line y = x swaps the coordinates.
        Matrix2D mirror = Matrix2D.Reflection(new Point2D(0, 0), new Point2D(1, 1));
        Point2D reflected = mirror.Transform(new Point2D(3, 1));

        reflected.X.Should().BeApproximately(1, 1e-9);
        reflected.Y.Should().BeApproximately(3, 1e-9);
        mirror.Determinant.Should().BeApproximately(-1, 1e-9);
    }

    [Fact]
    public void ArcEntity_Reflection_FlipsSweepDirection()
    {
        var arc = new ArcEntity(Point2D.Origin, 5, 0, GeometryMath.DegreesToRadians(90));

        arc.Transform(Matrix2D.Reflection(Point2D.Origin, new Point2D(1, 0))); // mirror over X axis

        arc.StartAngle.Should().BeApproximately(0, 1e-9);
        arc.SweepAngle.Should().BeApproximately(-GeometryMath.DegreesToRadians(90), 1e-9);
    }

    [Fact]
    public void ArcEntity_MirroredTwice_ReturnsToOriginal()
    {
        var arc = new ArcEntity(new Point2D(2, 3), 5, GeometryMath.DegreesToRadians(30), GeometryMath.DegreesToRadians(120));
        Point2D start0 = arc.PointAt(arc.StartAngle);
        Point2D end0 = arc.PointAt(arc.StartAngle + arc.SweepAngle);

        Matrix2D mirror = Matrix2D.Reflection(new Point2D(1, 1), new Point2D(4, 2));
        arc.Transform(mirror);
        arc.Transform(mirror);

        arc.Center.X.Should().BeApproximately(2, 1e-9);
        arc.Center.Y.Should().BeApproximately(3, 1e-9);
        arc.PointAt(arc.StartAngle).X.Should().BeApproximately(start0.X, 1e-9);
        arc.PointAt(arc.StartAngle).Y.Should().BeApproximately(start0.Y, 1e-9);
        arc.PointAt(arc.StartAngle + arc.SweepAngle).X.Should().BeApproximately(end0.X, 1e-9);
        arc.PointAt(arc.StartAngle + arc.SweepAngle).Y.Should().BeApproximately(end0.Y, 1e-9);
    }
}
