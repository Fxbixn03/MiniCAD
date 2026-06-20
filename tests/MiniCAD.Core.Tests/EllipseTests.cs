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

public class EllipseTests
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
    public void PointAt_GivesAxisEndpoints()
    {
        var ellipse = new EllipseEntity(new Point2D(0, 0), 10, 4);

        ellipse.PointAt(0).Should().Be(new Point2D(10, 0));
        ellipse.PointAt(Math.PI / 2).X.Should().BeApproximately(0, Tol);
        ellipse.PointAt(Math.PI / 2).Y.Should().BeApproximately(4, Tol);
    }

    [Fact]
    public void Bounds_OfAxisAlignedEllipse_MatchAxes()
    {
        var ellipse = new EllipseEntity(new Point2D(0, 0), 10, 4);
        Rect2D bounds = ellipse.Bounds;

        bounds.MinX.Should().BeApproximately(-10, 1e-6);
        bounds.MaxX.Should().BeApproximately(10, 1e-6);
        bounds.MinY.Should().BeApproximately(-4, 1e-6);
        bounds.MaxY.Should().BeApproximately(4, 1e-6);
    }

    [Fact]
    public void HitTest_OnOutlineButNotCentre()
    {
        var ellipse = new EllipseEntity(new Point2D(0, 0), 10, 4);

        ellipse.HitTest(new Point2D(10, 0), 0.1).Should().BeTrue();
        ellipse.HitTest(new Point2D(0, 0), 0.1).Should().BeFalse();
    }

    [Fact]
    public void Transform_TranslatesAndScales()
    {
        var ellipse = new EllipseEntity(new Point2D(1, 1), 10, 4);

        ellipse.Transform(Matrix2D.Scaling(2, 2, Point2D.Origin));

        ellipse.Center.Should().Be(new Point2D(2, 2));
        ellipse.RadiusX.Should().BeApproximately(20, Tol);
        ellipse.RadiusY.Should().BeApproximately(8, Tol);
    }

    [Fact]
    public void EllipseTool_ThreeClicks_BuildsEllipse()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new EllipseTool();
        tool.Activate(ctx);

        tool.PointerDown(Click(0, 0));    // centre
        tool.PointerDown(Click(10, 0));   // major axis end -> rx 10, rotation 0
        tool.PointerDown(Click(0, 4));    // minor extent -> ry 4

        var ellipse = doc.Entities.Should().ContainSingle().Which.Should().BeOfType<EllipseEntity>().Subject;
        ellipse.RadiusX.Should().BeApproximately(10, Tol);
        ellipse.RadiusY.Should().BeApproximately(4, Tol);
        ellipse.Rotation.Should().BeApproximately(0, Tol);
    }

    [Fact]
    public void Ellipse_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new EllipseEntity(new Point2D(2, 3), 9, 5, 0.5, 0.0, Math.PI));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var ellipse = restored.Entities.OfType<EllipseEntity>().Should().ContainSingle().Subject;
        ellipse.Center.Should().Be(new Point2D(2, 3));
        ellipse.RadiusX.Should().Be(9);
        ellipse.RadiusY.Should().Be(5);
        ellipse.Rotation.Should().BeApproximately(0.5, Tol);
        ellipse.SweepAngle.Should().BeApproximately(Math.PI, Tol);
    }
}
