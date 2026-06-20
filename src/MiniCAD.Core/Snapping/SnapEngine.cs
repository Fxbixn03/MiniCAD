using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Snapping;

/// <summary>
/// Resolves a free cursor point to the nearest snap target. Object snaps (entity points)
/// take priority over the grid; everything is evaluated within a screen-constant tolerance
/// so snapping feels the same at every zoom level.
/// </summary>
public sealed class SnapEngine
{
    /// <summary>Snap capture radius in device pixels.</summary>
    private const double SnapPixelRadius = 12.0;

    private readonly ICadDocument _document;
    private readonly Viewport _viewport;
    private readonly SnapSettings _settings;

    public SnapEngine(ICadDocument document, Viewport viewport, SnapSettings settings)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public SnapResult Snap(Point2D world)
    {
        if (!_settings.Enabled)
            return SnapResult.Unsnapped(world);

        double scale = Math.Max(_viewport.Scale, Viewport.MinScale);
        double tolerance = SnapPixelRadius / scale;

        if (_settings.SnapToPoints && TryObjectSnap(world, tolerance * tolerance, out Point2D point))
            return new SnapResult(point, SnapKind.Point);

        if (_settings.SnapToGrid && TryGridSnap(world, scale, tolerance, out Point2D gridPoint))
            return new SnapResult(gridPoint, SnapKind.Grid);

        return SnapResult.Unsnapped(world);
    }

    private bool TryObjectSnap(Point2D world, double toleranceSquared, out Point2D best)
    {
        best = default;
        double bestSquared = toleranceSquared;
        bool found = false;

        foreach (IEntity entity in _document.Entities)
        {
            if (!_document.IsEntityVisible(entity))
                continue;

            foreach (Point2D candidate in entity.SnapPoints)
            {
                double distanceSquared = world.DistanceSquaredTo(candidate);
                if (distanceSquared <= bestSquared)
                {
                    bestSquared = distanceSquared;
                    best = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private static bool TryGridSnap(Point2D world, double scale, double tolerance, out Point2D snapped)
    {
        double step = GridMetrics.StepFor(scale);
        snapped = new Point2D(Math.Round(world.X / step) * step, Math.Round(world.Y / step) * step);
        return world.DistanceTo(snapped) <= tolerance;
    }
}
