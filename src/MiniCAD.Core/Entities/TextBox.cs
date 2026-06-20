using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Shared geometry for the approximate, rotation-aware bounding box of annotation text
/// (<see cref="TextEntity"/> / <see cref="MTextEntity"/>). The box is computed in the
/// unrotated text-local frame (origin at the insertion point) and then mapped to world space,
/// keeping picking/snapping free of any real font metrics.
/// </summary>
internal static class TextBox
{
    /// <summary>The text box in the local frame, anchored around the origin per the alignment.</summary>
    public static Rect2D Local(double width, double height, TextHAlign horizontal, TextVAlign vertical)
    {
        (double minX, double maxX) = horizontal switch
        {
            TextHAlign.Center => (-width * 0.5, width * 0.5),
            TextHAlign.Right => (-width, 0.0),
            _ => (0.0, width),
        };
        (double minY, double maxY) = vertical switch
        {
            TextVAlign.Top => (-height, 0.0),
            TextVAlign.Middle => (-height * 0.5, height * 0.5),
            _ => (0.0, height), // Baseline / Bottom: text sits above the anchor line
        };
        return new Rect2D(minX, minY, maxX, maxY);
    }

    /// <summary>The four world-space corners of <paramref name="local"/> under <paramref name="toWorld"/>.</summary>
    public static Point2D[] Corners(Rect2D local, in Matrix2D toWorld) => new[]
    {
        toWorld.Transform(new Point2D(local.MinX, local.MinY)),
        toWorld.Transform(new Point2D(local.MaxX, local.MinY)),
        toWorld.Transform(new Point2D(local.MaxX, local.MaxY)),
        toWorld.Transform(new Point2D(local.MinX, local.MaxY)),
    };

    /// <summary>The axis-aligned bounding box enclosing <paramref name="corners"/>.</summary>
    public static Rect2D Bounds(Point2D[] corners)
    {
        Rect2D bounds = Rect2D.FromPoints(corners[0], corners[0]);
        for (int i = 1; i < corners.Length; i++)
            bounds = bounds.Union(corners[i]);
        return bounds;
    }

    /// <summary>True if <paramref name="world"/> lies within <paramref name="tolerance"/> of the rotated box.</summary>
    public static bool Contains(Rect2D local, in Matrix2D toWorld, Point2D world, double tolerance)
    {
        if (!toWorld.TryInvert(out Matrix2D toLocal))
            return false;

        Point2D p = toLocal.Transform(world);
        return p.X >= local.MinX - tolerance && p.X <= local.MaxX + tolerance
            && p.Y >= local.MinY - tolerance && p.Y <= local.MaxY + tolerance;
    }

    /// <summary>True if the (rotated) box crosses or contains <paramref name="rect"/>, or vice versa.</summary>
    public static bool IntersectsRect(Point2D[] corners, Rect2D rect)
    {
        for (int i = 0; i < corners.Length; i++)
        {
            Point2D a = corners[i];
            Point2D b = corners[(i + 1) % corners.Length];
            if (GeometryMath.SegmentIntersectsRect(a, b, rect))
                return true;
        }

        // The box may fully enclose the rect without any edge crossing it.
        return ContainsPoint(corners, rect.Center);
    }

    /// <summary>Even-odd point-in-polygon test for the (convex) corner ring.</summary>
    private static bool ContainsPoint(Point2D[] corners, Point2D p)
    {
        bool inside = false;
        for (int i = 0, j = corners.Length - 1; i < corners.Length; j = i++)
        {
            Point2D a = corners[i];
            Point2D b = corners[j];
            if (a.Y > p.Y != b.Y > p.Y &&
                p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
