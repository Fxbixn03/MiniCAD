using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class ViewportTests
{
    private static Viewport CreateViewport()
    {
        var viewport = new Viewport();
        viewport.Resize(800, 600);
        return viewport;
    }

    [Fact]
    public void WorldToScreen_AndBack_RoundTrips()
    {
        Viewport viewport = CreateViewport();
        viewport.ZoomToFit(new Rect2D(-10, -10, 10, 10));
        var world = new Point2D(3, -4);

        Point2D screen = viewport.WorldToScreenPoint(world);
        Point2D roundTrip = viewport.ScreenToWorld(screen);

        roundTrip.X.Should().BeApproximately(world.X, 1e-9);
        roundTrip.Y.Should().BeApproximately(world.Y, 1e-9);
    }

    [Fact]
    public void WorldToScreen_FlipsYAxis()
    {
        Viewport viewport = CreateViewport();
        viewport.ZoomToFit(new Rect2D(-10, -10, 10, 10));

        Point2D above = viewport.WorldToScreenPoint(new Point2D(0, 5));
        Point2D below = viewport.WorldToScreenPoint(new Point2D(0, -5));

        // Higher world Y must map to a smaller screen Y (screen origin is top-left).
        above.Y.Should().BeLessThan(below.Y);
    }

    [Fact]
    public void ZoomAt_KeepsWorldPointUnderCursor()
    {
        Viewport viewport = CreateViewport();
        var cursor = new Point2D(200, 150);
        Point2D worldBefore = viewport.ScreenToWorld(cursor);

        viewport.ZoomAt(cursor, 2.5);
        Point2D worldAfter = viewport.ScreenToWorld(cursor);

        worldAfter.X.Should().BeApproximately(worldBefore.X, 1e-9);
        worldAfter.Y.Should().BeApproximately(worldBefore.Y, 1e-9);
    }

    [Fact]
    public void ZoomToFit_CentersOnContent()
    {
        Viewport viewport = CreateViewport();
        var bounds = new Rect2D(10, 20, 30, 40);

        viewport.ZoomToFit(bounds);

        viewport.Center.Should().Be(bounds.Center);
    }

    [Fact]
    public void Resize_RaisesChanged_OnlyWhenSizeDiffers()
    {
        var viewport = new Viewport();
        int raised = 0;
        viewport.Changed += (_, _) => raised++;

        viewport.Resize(800, 600);
        viewport.Resize(800, 600);

        raised.Should().Be(1);
    }
}
