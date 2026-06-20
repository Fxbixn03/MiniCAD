using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class ArrayTests
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

    [Fact]
    public void Rectangular_ExcludesOriginal_AndPlacesGrid()
    {
        IReadOnlyList<Matrix2D> transforms = ArrayGenerator.Rectangular(2, 3, 10, 5);

        transforms.Should().HaveCount(5); // 2*3 - 1
        transforms.Should().Contain(t => Close(t.Transform(Point2D.Origin), new Point2D(20, 0)));
        transforms.Should().Contain(t => Close(t.Transform(Point2D.Origin), new Point2D(10, 5)));
    }

    [Fact]
    public void Polar_SpreadsCopiesEvenlyAroundCentre()
    {
        IReadOnlyList<Matrix2D> transforms = ArrayGenerator.Polar(Point2D.Origin, 4, Math.PI * 2.0);

        transforms.Should().HaveCount(3); // count - 1
        Point2D rotated = transforms[0].Transform(new Point2D(1, 0)); // 90°
        rotated.X.Should().BeApproximately(0, Tol);
        rotated.Y.Should().BeApproximately(1, Tol);
    }

    [Fact]
    public void ArrayTool_Rectangular_AddsCopies_AsOneUndoableStep()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var line = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
        doc.AddEntity(line);
        ctx.Selection.Add(line);

        var tool = new ArrayTool { Rows = 2, Columns = 2, SpacingX = 10, SpacingY = 10, Polar = false };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });

        doc.Entities.Should().HaveCount(4); // original + 3 copies
        ctx.Commands.Undo();
        doc.Entities.Should().ContainSingle().Which.Should().BeSameAs(line);
    }

    private static bool Close(Point2D a, Point2D b) => a.DistanceTo(b) <= Tol;
}
