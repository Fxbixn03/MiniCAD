using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Which line of a thick element (wall, beam) the user draws along — the Allplan "Anschlag".
/// With <see cref="Center"/> the picked points are the centerline (the default); with
/// <see cref="Left"/>/<see cref="Right"/> they are a face, and the body sits to one side.
/// </summary>
public enum DrawReference
{
    Center,
    Left,
    Right,
}

/// <summary>Maps a reference line to the perpendicular offset that yields the element centerline.</summary>
public static class DrawReferenceMath
{
    /// <summary>
    /// The vector to add to both picked points to obtain the centerline, given the drawing
    /// direction <paramref name="start"/>→<paramref name="end"/>, the element thickness and the
    /// chosen reference. "Left"/"Right" are relative to the drawing direction.
    /// </summary>
    public static Vector2D CenterlineOffset(Point2D start, Point2D end, double thickness, DrawReference reference)
    {
        if (reference == DrawReference.Center)
            return Vector2D.Zero;

        Vector2D dir = end - start;
        if (dir.Length <= GeometryMath.Epsilon)
            return Vector2D.Zero;
        dir = dir.Normalized();

        // Left of travel (Y up); the body extends opposite the face the user draws along.
        var left = new Vector2D(-dir.Y, dir.X);
        double half = thickness / 2;
        return reference == DrawReference.Left ? left * -half : left * half;
    }
}
