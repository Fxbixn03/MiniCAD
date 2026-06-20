using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class InputConstraintTests
{
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;

        public ICadDocument Document { get; }

        public Selection Selection { get; } = new();

        public double PickTolerance { get; set; } = 0.5;

        public StrokeStyle? DefaultStrokeOverride { get; set; }

        public InputSettings InputSettings { get; } = new();

        public UndoRedoManager Commands { get; } = new();

        public void Execute(IUndoableCommand command) => Commands.Execute(command);

        public void Push(IUndoableCommand command) => Commands.Push(command);

        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world); // no object snap

        public void RequestRedraw()
        {
        }

        public void ReturnToDefaultTool()
        {
        }
    }

    private static ToolPointerInput Click(double x, double y, ToolModifiers modifiers = ToolModifiers.None)
        => new() { World = new Point2D(x, y), Button = ToolButton.Left, Modifiers = modifiers };

    private static LineEntity DrawSegment(TestContext ctx, Point2D start, Point2D second, ToolModifiers modifiers = ToolModifiers.None)
    {
        var tool = new LineTool();
        tool.Activate(ctx);
        tool.PointerDown(Click(start.X, start.Y));
        tool.PointerDown(Click(second.X, second.Y, modifiers));
        return (LineEntity)ctx.Document.Entities[^1];
    }

    [Theory]
    [InlineData(false, false, false, null)] // nothing -> free
    [InlineData(false, false, true, 90.0)]  // shift -> ortho
    [InlineData(true, false, false, 90.0)]  // ortho on
    [InlineData(true, false, true, null)]   // ortho on, shift toggles it off
    [InlineData(false, true, false, 30.0)]  // polar on (step 30)
    public void AngleStep_FollowsOrthoPolarAndShift(bool ortho, bool polar, bool shift, double? expected)
    {
        var settings = new InputSettings { OrthoEnabled = ortho, PolarEnabled = polar, PolarAngleStepDegrees = 30 };

        settings.AngleStepDegrees(shift).Should().Be(expected);
    }

    [Fact]
    public void Ortho_LocksSegmentToHorizontalOrVertical()
    {
        var ctx = new TestContext(new CadDocument());
        ctx.InputSettings.OrthoEnabled = true;

        LineEntity line = DrawSegment(ctx, new Point2D(0, 0), new Point2D(10, 3));

        line.End.Y.Should().BeApproximately(0, 1e-9);                         // snapped to horizontal
        line.End.X.Should().BeApproximately(Math.Sqrt(109), 1e-9);           // distance preserved
    }

    [Fact]
    public void Polar_SnapsToConfiguredAngleStep()
    {
        var ctx = new TestContext(new CadDocument());
        ctx.InputSettings.PolarEnabled = true;
        ctx.InputSettings.PolarAngleStepDegrees = 45;

        LineEntity line = DrawSegment(ctx, new Point2D(0, 0), new Point2D(10, 8)); // ~38.7° -> 45°

        line.End.X.Should().BeApproximately(line.End.Y, 1e-9);               // on the 45° ray
        double length = Math.Sqrt(10 * 10 + 8 * 8);
        line.End.X.Should().BeApproximately(length / Math.Sqrt(2), 1e-9);    // distance preserved
    }

    [Fact]
    public void NoConstraint_LeavesPointFree()
    {
        var ctx = new TestContext(new CadDocument());

        LineEntity line = DrawSegment(ctx, new Point2D(0, 0), new Point2D(10, 3));

        line.End.Should().Be(new Point2D(10, 3));
    }

    [Fact]
    public void Shift_TogglesOrthoOff_WhenAlreadyOn()
    {
        var ctx = new TestContext(new CadDocument());
        ctx.InputSettings.OrthoEnabled = true;

        LineEntity line = DrawSegment(ctx, new Point2D(0, 0), new Point2D(10, 3), ToolModifiers.Shift);

        line.End.Should().Be(new Point2D(10, 3)); // Shift suspended ortho -> free point
    }
}
