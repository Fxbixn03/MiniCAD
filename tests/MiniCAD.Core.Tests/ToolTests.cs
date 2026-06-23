using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class ToolTests
{
    /// <summary>Minimal in-memory context so tools can be exercised without any UI.</summary>
    private sealed class TestToolContext : IToolContext
    {
        private readonly UndoRedoManager _commands = new();

        public TestToolContext(CadDocument document) => Document = document;

        public ICadDocument Document { get; }

        public Selection Selection { get; } = new();

        public double PickTolerance { get; set; } = 0.5;

        public StrokeStyle? DefaultStrokeOverride { get; set; }

        public int RedrawCount { get; private set; }

        public int ReturnToDefaultCount { get; private set; }

        public UndoRedoManager Commands => _commands;

        public void Execute(IUndoableCommand command) => _commands.Execute(command);

        public void Push(IUndoableCommand command) => _commands.Push(command);

        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);

        public void RequestRedraw() => RedrawCount++;

        public void ReturnToDefaultTool() => ReturnToDefaultCount++;
    }

    private static ToolPointerInput Click(double x, double y, ToolButton button = ToolButton.Left, int clickCount = 1)
        => new() { World = new Point2D(x, y), Button = button, ClickCount = clickCount };

    private static (CadDocument doc, TestToolContext ctx) Setup<TTool>(out TTool tool) where TTool : ToolBase, new()
    {
        var doc = new CadDocument();
        var ctx = new TestToolContext(doc);
        tool = new TTool();
        tool.Activate(ctx);
        return (doc, ctx);
    }

    [Fact]
    public void LineTool_TwoClicks_AddsLine()
    {
        (CadDocument doc, _) = Setup<LineTool>(out LineTool tool);

        tool.PointerDown(Click(0, 0));
        tool.PointerMove(Click(5, 5, ToolButton.None));
        tool.PointerDown(Click(10, 0));

        doc.Entities.Should().ContainSingle();
        var line = doc.Entities[0].Should().BeOfType<LineEntity>().Subject;
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Should().Be(new Point2D(10, 0));
    }

    [Fact]
    public void LineTool_ShowsPreview_AfterFirstClick()
    {
        Setup<LineTool>(out LineTool tool);

        tool.GetOverlay().Should().BeEmpty();
        tool.PointerDown(Click(0, 0));
        tool.PointerMove(Click(4, 3, ToolButton.None));

        tool.GetOverlay().Should().ContainSingle();
    }

    [Fact]
    public void LineTool_WithShift_ConstrainsToOrtho()
    {
        (CadDocument doc, _) = Setup<LineTool>(out LineTool tool);

        tool.PointerDown(Click(0, 0));
        // Second point is below-and-right but Shift should lock the line to horizontal.
        tool.PointerDown(Click(10, 3) with { Modifiers = ToolModifiers.Shift });

        var line = doc.Entities.Should().ContainSingle().Subject.Should().BeOfType<LineEntity>().Subject;
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Y.Should().BeApproximately(0, 1e-9);
        line.End.X.Should().BeApproximately(Math.Sqrt(10 * 10 + 3 * 3), 1e-9);
    }

    [Fact]
    public void LineTool_Escape_CancelsWithoutAdding()
    {
        (CadDocument doc, _) = Setup<LineTool>(out LineTool tool);

        tool.PointerDown(Click(0, 0));
        tool.KeyDown(ToolKey.Escape);
        tool.GetOverlay().Should().BeEmpty();
        doc.Entities.Should().BeEmpty();
    }

    [Fact]
    public void CircleTool_CenterThenRadius_AddsCircle()
    {
        (CadDocument doc, _) = Setup<CircleTool>(out CircleTool tool);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(3, 4));

        var circle = doc.Entities.Should().ContainSingle().Subject.Should().BeOfType<CircleEntity>().Subject;
        circle.Radius.Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public void RectangleTool_TwoCorners_AddsClosedPolyline()
    {
        (CadDocument doc, _) = Setup<RectangleTool>(out RectangleTool tool);

        tool.PointerDown(Click(2, 1));
        tool.PointerDown(Click(-1, 4));

        var poly = doc.Entities.Should().ContainSingle().Subject.Should().BeOfType<PolylineEntity>().Subject;
        poly.IsClosed.Should().BeTrue();
        poly.Bounds.Should().Be(new Rect2D(-1, 1, 2, 4));
    }

    [Fact]
    public void PolylineTool_FinishesOnDoubleClick()
    {
        (CadDocument doc, _) = Setup<PolylineTool>(out PolylineTool tool);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(5, 0));
        tool.PointerDown(Click(5, 5, ToolButton.Left, clickCount: 2)); // double-click finishes

        var poly = doc.Entities.Should().ContainSingle().Subject.Should().BeOfType<PolylineEntity>().Subject;
        poly.Points.Should().HaveCount(2);
        poly.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void PolylineTool_RightClick_Finishes()
    {
        (CadDocument doc, _) = Setup<PolylineTool>(out PolylineTool tool);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(1, 0));
        tool.PointerDown(Click(2, 2));
        tool.PointerDown(Click(0, 0, ToolButton.Right));

        doc.Entities.Should().ContainSingle();
        ((PolylineEntity)doc.Entities[0]).Points.Should().HaveCount(3);
    }

    [Fact]
    public void SelectTool_ClickSelectsTopmostEntity()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(line);

        tool.PointerDown(Click(5, 0));

        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(line);
    }

    [Fact]
    public void SelectTool_DoesNotPick_EntityOnLockedLayer()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        Layer layer = doc.AddLayer("Referenz", StrokeStyle.Default);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0)) { LayerId = layer.Id };
        doc.AddEntity(line);
        doc.SetLayerState(layer, ElementState.Locked);

        tool.PointerDown(Click(5, 0));

        ctx.Selection.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void SelectTool_DragMovesSelection_AsSingleUndoStep()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(line);

        tool.PointerDown(Click(5, 0));               // select + begin drag
        tool.PointerMove(Click(5, 3, ToolButton.None));
        tool.PointerMove(Click(5, 5, ToolButton.None));
        tool.PointerUp(Click(5, 5, ToolButton.None));

        line.Start.Should().Be(new Point2D(0, 5));
        ctx.Commands.CanUndo.Should().BeTrue();

        ctx.Commands.Undo();
        line.Start.Should().Be(new Point2D(0, 0));   // single step restores the original
    }

    [Fact]
    public void SelectTool_Delete_RemovesSelectionInOneStep()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var a = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var b = new LineEntity(new Point2D(0, 0), new Point2D(0, 10));
        doc.AddEntity(a);
        doc.AddEntity(b);

        tool.PointerDown(Click(5, 0));
        tool.PointerDown(Click(0, 5, ToolButton.Left) with { Modifiers = ToolModifiers.Shift });
        ctx.Selection.Count.Should().Be(2);

        tool.KeyDown(ToolKey.Delete);

        doc.Entities.Should().BeEmpty();
        ctx.Commands.Undo();
        doc.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void ToolManager_RemovingEntity_DropsItFromSelection()
    {
        var doc = new CadDocument();
        var commands = new UndoRedoManager();
        var viewport = new Viewport();
        viewport.Resize(800, 600);
        var manager = new ToolManager(doc, commands, viewport);

        var line = new LineEntity(new Point2D(0, 0), new Point2D(1, 1));
        doc.AddEntity(line);
        manager.Selection.Set(line);

        doc.RemoveEntity(line);

        manager.Selection.IsEmpty.Should().BeTrue();
    }

    private static ToolManager CreateManager(out CadDocument document)
    {
        document = new CadDocument();
        var viewport = new Viewport();
        viewport.Resize(800, 600); // Scale stays 1 -> pick tolerance is 6 world units
        return new ToolManager(document, new UndoRedoManager(), viewport);
    }

    private static ToolPointerInput DoubleRightClick(double x, double y)
        => new() { World = new Point2D(x, y), Button = ToolButton.Right, ClickCount = 2 };

    [Fact]
    public void QuickSelect_AdoptsLayerAndTeilbild_AndActivatesMatchingTool()
    {
        ToolManager manager = CreateManager(out CadDocument doc);
        var lineTool = new LineTool();
        manager.RegisterQuickSelectTool<LineEntity>(lineTool);
        manager.SetActiveTool(new SelectTool());

        Layer detail = doc.AddLayer("Detail", new StrokeStyle(Color.Cyan, 1));
        PartialDrawing teilbild2 = doc.AddPartialDrawing("Teilbild 2");
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0))
        {
            LayerId = detail.Id,
            PartialDrawingId = teilbild2.Id,
        };
        doc.AddEntity(line);

        manager.PointerDown(DoubleRightClick(5, 0));

        manager.ActiveTool.Should().Be(lineTool);
        doc.ActiveLayer.Should().Be(detail);
        doc.ActivePartialDrawing.Should().Be(teilbild2);
    }

    [Fact]
    public void QuickSelect_OnUnregisteredType_SelectsEntity()
    {
        ToolManager manager = CreateManager(out CadDocument doc);
        manager.SetActiveTool(new SelectTool());

        var arc = new ArcEntity(Point2D.Origin, 5, 0, GeometryMath.DegreesToRadians(90));
        doc.AddEntity(arc);

        manager.PointerDown(DoubleRightClick(5, 0)); // (5,0) is on the arc at angle 0

        manager.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(arc);
    }

    [Fact]
    public void QuickSelect_OnEmptySpace_DoesNothing()
    {
        ToolManager manager = CreateManager(out CadDocument doc);
        var lineTool = new LineTool();
        var selectTool = new SelectTool();
        manager.RegisterQuickSelectTool<LineEntity>(lineTool);
        manager.SetActiveTool(selectTool);
        doc.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(10, 0)));

        manager.PointerDown(DoubleRightClick(500, 500)); // far from the line

        manager.ActiveTool.Should().Be(selectTool);
    }

    [Fact]
    public void DrawingTool_AppliesDefaultStrokeOverride_ToNewEntity()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<LineTool>(out LineTool tool);
        ctx.DefaultStrokeOverride = new StrokeStyle(Color.Red, 3.0);

        tool.PointerDown(Click(0, 0));
        tool.PointerDown(Click(10, 0));

        doc.Entities.Should().ContainSingle();
        doc.Entities[0].StrokeOverride.Should().Be(new StrokeStyle(Color.Red, 3.0));
    }

    [Fact]
    public void DrawingTool_EscapeWhileIdle_ReturnsToDefaultTool()
    {
        (_, TestToolContext ctx) = Setup<LineTool>(out LineTool tool);

        tool.KeyDown(ToolKey.Escape); // nothing in progress

        ctx.ReturnToDefaultCount.Should().Be(1);
    }

    [Fact]
    public void DrawingTool_EscapeMidConstruction_CancelsWithoutLeavingTool()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<LineTool>(out LineTool tool);

        tool.PointerDown(Click(0, 0)); // first point placed
        tool.KeyDown(ToolKey.Escape);  // abort the in-progress line

        ctx.ReturnToDefaultCount.Should().Be(0);
        doc.Entities.Should().BeEmpty();
    }

    [Fact]
    public void CommitCoordinate_PlacesExactPoints_IgnoringSnap_AndTracksLastPoint()
    {
        ToolManager manager = CreateManager(out CadDocument doc);
        manager.SetActiveTool(new LineTool());

        // Points near the grid would normally snap; typed coordinates must stay exact.
        manager.CommitCoordinate(new Point2D(2, 3));
        manager.CommitCoordinate(new Point2D(7, 9));

        var line = doc.Entities.Should().ContainSingle().Which.Should().BeOfType<LineEntity>().Subject;
        line.Start.Should().Be(new Point2D(2, 3));
        line.End.Should().Be(new Point2D(7, 9));
        manager.LastPoint.Should().Be(new Point2D(7, 9));
        manager.SnapSettings.Enabled.Should().BeTrue(); // snapping restored afterwards
    }

    private static void BoxSelect(SelectTool tool, Point2D from, Point2D to, bool shift = false)
    {
        var modifiers = shift ? ToolModifiers.Shift : ToolModifiers.None;
        tool.PointerDown(Click(from.X, from.Y) with { Modifiers = modifiers });
        tool.PointerMove(Click(to.X, to.Y, ToolButton.None));
        tool.PointerUp(Click(to.X, to.Y, ToolButton.None) with { Modifiers = modifiers });
    }

    [Fact]
    public void WindowSelection_LeftToRight_SelectsOnlyFullyEnclosed()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var inside = new LineEntity(new Point2D(1, 1), new Point2D(3, 3));
        var sticksOut = new LineEntity(new Point2D(4, 4), new Point2D(20, 20));
        doc.AddEntity(inside);
        doc.AddEntity(sticksOut);

        BoxSelect(tool, new Point2D(0, 0), new Point2D(10, 10)); // left -> right = window

        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(inside);
    }

    [Fact]
    public void CrossingSelection_RightToLeft_SelectsIntersected()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var inside = new LineEntity(new Point2D(1, 1), new Point2D(3, 3));
        var sticksOut = new LineEntity(new Point2D(4, 4), new Point2D(20, 20));
        doc.AddEntity(inside);
        doc.AddEntity(sticksOut);

        // Start corner kept clear of the diagonal line so it begins a box (not a pick).
        BoxSelect(tool, new Point2D(15, 10), new Point2D(0, 0)); // right -> left = crossing

        ctx.Selection.Items.Should().HaveCount(2);
    }

    [Fact]
    public void BoxSelection_WithShift_ExtendsSelection()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var a = new LineEntity(new Point2D(0, 0), new Point2D(2, 0));
        var b = new LineEntity(new Point2D(0, 5), new Point2D(2, 5));
        doc.AddEntity(a);
        doc.AddEntity(b);

        BoxSelect(tool, new Point2D(-1, -1), new Point2D(3, 1));            // window-select a
        BoxSelect(tool, new Point2D(-1, 4), new Point2D(3, 6), shift: true); // shift-add b

        ctx.Selection.Items.Should().HaveCount(2);
    }

    [Fact]
    public void BoxSelection_SkipsEntitiesOnLockedLayer()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        Layer layer = doc.AddLayer("Referenz", StrokeStyle.Default);
        var line = new LineEntity(new Point2D(1, 1), new Point2D(3, 3)) { LayerId = layer.Id };
        doc.AddEntity(line);
        doc.SetLayerState(layer, ElementState.Locked);

        BoxSelect(tool, new Point2D(0, 0), new Point2D(10, 10));

        ctx.Selection.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ClickOnEmptySpace_ClearsSelection()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(line);

        tool.PointerDown(Click(5, 0));            // select
        tool.PointerUp(Click(5, 0, ToolButton.None));
        ctx.Selection.Count.Should().Be(1);

        tool.PointerDown(Click(50, 50));          // empty space -> rubber band
        tool.PointerUp(Click(50, 50, ToolButton.None)); // no drag -> treated as a click
        ctx.Selection.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void OffsetTool_Line_CreatesParallelCopyOnCursorSide()
    {
        (CadDocument doc, _) = Setup<OffsetTool>(out OffsetTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(line);

        tool.PointerDown(Click(5, 0));   // pick the line
        tool.PointerDown(Click(5, 3));   // through point 3 units above

        doc.Entities.Should().HaveCount(2);
        var copy = doc.Entities[1].Should().BeOfType<LineEntity>().Subject;
        copy.Start.Should().Be(new Point2D(0, 3));
        copy.End.Should().Be(new Point2D(10, 3));
    }

    [Fact]
    public void OffsetTool_Circle_CreatesConcentricCircleThroughCursor()
    {
        (CadDocument doc, _) = Setup<OffsetTool>(out OffsetTool tool);
        var circle = new CircleEntity(new Point2D(0, 0), 5);
        doc.AddEntity(circle);

        tool.PointerDown(Click(5, 0));   // pick on the ring
        tool.PointerDown(Click(8, 0));   // through point -> radius 8

        var copy = doc.Entities[1].Should().BeOfType<CircleEntity>().Subject;
        copy.Center.Should().Be(new Point2D(0, 0));
        copy.Radius.Should().BeApproximately(8, 1e-9);
    }

    [Fact]
    public void OffsetTool_ClosedRectangle_OffsetsOutward()
    {
        (CadDocument doc, _) = Setup<OffsetTool>(out OffsetTool tool);
        var rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(10, 10));
        doc.AddEntity(rect);

        tool.PointerDown(Click(5, 0));    // pick the bottom edge
        tool.PointerDown(Click(5, -2));   // through point below -> outward by 2

        var copy = doc.Entities[1].Should().BeOfType<PolylineEntity>().Subject;
        copy.IsClosed.Should().BeTrue();
        copy.Points.Should().HaveCount(4);
        copy.Bounds.Should().Be(new Rect2D(-2, -2, 12, 12));
    }

    [Fact]
    public void OffsetGeometry_ThroughPointOnLine_ReturnsNull()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));

        OffsetGeometry.Offset(line, new Point2D(4, 0)).Should().BeNull();
    }

    /// <summary>Runs the four-click stretch gesture: window corners, then base and target.</summary>
    private static void StretchGesture(StretchTool tool, Point2D c1, Point2D c2, Point2D basePoint, Point2D target)
    {
        tool.PointerDown(Click(c1.X, c1.Y));
        tool.PointerDown(Click(c2.X, c2.Y));
        tool.PointerDown(Click(basePoint.X, basePoint.Y));
        tool.PointerDown(Click(target.X, target.Y));
    }

    [Fact]
    public void StretchTool_MovesOnlyVerticesInsideWindow()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<StretchTool>(out StretchTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(line);

        // Window encloses only the right endpoint; drag it up by 4.
        StretchGesture(tool, new Point2D(8, -2), new Point2D(12, 2), new Point2D(10, 0), new Point2D(10, 4));

        line.Start.Should().Be(new Point2D(0, 0));   // outside the window: fixed
        line.End.Should().Be(new Point2D(10, 4));    // inside the window: moved

        ctx.Commands.Undo();
        line.End.Should().Be(new Point2D(10, 0));    // single undoable step
    }

    [Fact]
    public void StretchTool_FullyEnclosed_MovesWholeObject()
    {
        (CadDocument doc, _) = Setup<StretchTool>(out StretchTool tool);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(2, 0));
        doc.AddEntity(line);

        // Window encloses both endpoints -> the whole line shifts by the displacement.
        StretchGesture(tool, new Point2D(-1, -1), new Point2D(3, 1), new Point2D(0, 0), new Point2D(5, 5));

        line.Start.Should().Be(new Point2D(5, 5));
        line.End.Should().Be(new Point2D(7, 5));
    }

    [Fact]
    public void SelectTool_RepeatedClickSameSpot_CyclesOverlappingObjects()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var lower = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var upper = new LineEntity(new Point2D(0, 0), new Point2D(8, 0));
        doc.AddEntity(lower);
        doc.AddEntity(upper);

        // (2,0) lies on both lines but is no vertex/midpoint grip of either.
        ClickAt(tool, 2, 0);
        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(upper, "topmost first");

        ClickAt(tool, 2, 0);
        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(lower, "cycles to the one underneath");

        ClickAt(tool, 2, 0);
        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(upper, "wraps around");
    }

    [Fact]
    public void SelectTool_ClickingDifferentSpot_DoesNotCycle()
    {
        (CadDocument doc, TestToolContext ctx) = Setup<SelectTool>(out SelectTool tool);
        var lower = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        var upper = new LineEntity(new Point2D(0, 0), new Point2D(8, 0));
        doc.AddEntity(lower);
        doc.AddEntity(upper);

        ClickAt(tool, 2, 0);
        ClickAt(tool, 3, 0); // a new spot resets the cycle -> topmost again

        ctx.Selection.Items.Should().ContainSingle().Which.Should().BeSameAs(upper);
    }

    private static void ClickAt(SelectTool tool, double x, double y)
    {
        tool.PointerDown(Click(x, y));
        tool.PointerUp(Click(x, y, ToolButton.None));
    }
}
