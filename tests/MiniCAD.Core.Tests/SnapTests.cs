using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class SnapTests
{
    // A default viewport (Resize only) keeps Scale = 1, so the 12-px snap tolerance is
    // 12 world units and the grid step (GridMetrics.StepFor(1)) is 100 world units.
    private static (CadDocument doc, Viewport vp, SnapSettings settings, SnapEngine engine) Setup()
    {
        var doc = new CadDocument();
        var vp = new Viewport();
        vp.Resize(800, 600);
        var settings = new SnapSettings();
        return (doc, vp, settings, new SnapEngine(doc, vp, settings));
    }

    [Fact]
    public void Snap_ToEndpoint_WhenWithinTolerance()
    {
        (CadDocument doc, _, _, SnapEngine engine) = Setup();
        doc.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(50, 0)));

        SnapResult result = engine.Snap(new Point2D(3, 0));

        result.IsSnapped.Should().BeTrue();
        result.Kind.Should().Be(SnapKind.Point);
        result.Point.Should().Be(new Point2D(0, 0));
    }

    [Fact]
    public void Snap_ToGrid_WhenNoObjectInRange()
    {
        (_, _, _, SnapEngine engine) = Setup();

        SnapResult result = engine.Snap(new Point2D(4, 3));

        result.Kind.Should().Be(SnapKind.Grid);
        result.Point.Should().Be(new Point2D(0, 0));
    }

    [Fact]
    public void Snap_ObjectTakesPriorityOverGrid()
    {
        (CadDocument doc, _, _, SnapEngine engine) = Setup();
        doc.AddEntity(new LineEntity(new Point2D(4, 0), new Point2D(60, 0)));

        SnapResult result = engine.Snap(new Point2D(3, 0));

        result.Kind.Should().Be(SnapKind.Point);
        result.Point.Should().Be(new Point2D(4, 0));
    }

    [Fact]
    public void Snap_ReturnsUnsnapped_WhenDisabled()
    {
        (CadDocument doc, _, SnapSettings settings, SnapEngine engine) = Setup();
        doc.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(50, 0)));
        settings.Enabled = false;

        SnapResult result = engine.Snap(new Point2D(3, 0));

        result.IsSnapped.Should().BeFalse();
        result.Point.Should().Be(new Point2D(3, 0));
    }

    [Fact]
    public void Snap_DoesNotSnapToGrid_WhenGridSnapDisabled()
    {
        (_, _, SnapSettings settings, SnapEngine engine) = Setup();
        settings.SnapToGrid = false;

        // (4,3) is well within tolerance of the grid origin, but grid snapping is off.
        SnapResult result = engine.Snap(new Point2D(4, 3));

        result.IsSnapped.Should().BeFalse();
    }

    [Fact]
    public void Snap_ReturnsUnsnapped_WhenNothingInRange()
    {
        (_, _, SnapSettings settings, SnapEngine engine) = Setup();
        settings.SnapToGrid = false;

        SnapResult result = engine.Snap(new Point2D(40, 40));

        result.IsSnapped.Should().BeFalse();
    }

    [Fact]
    public void LineEntity_ExposesEndpointsAndMidpoint_AsSnapPoints()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));

        line.SnapPoints.Should().BeEquivalentTo(new[]
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(5, 0),
        });
    }
}
