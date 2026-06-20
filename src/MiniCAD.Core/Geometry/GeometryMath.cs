namespace MiniCAD.Core.Geometry;

/// <summary>
/// Shared numeric helpers for the geometry layer. Everything here is framework-agnostic
/// and works in double precision, which is the expected accuracy for CAD work.
/// </summary>
public static class GeometryMath
{
    /// <summary>Default tolerance used for geometric equality comparisons.</summary>
    public const double Epsilon = 1e-9;

    /// <summary>Returns <c>true</c> if two values are equal within <paramref name="tolerance"/>.</summary>
    public static bool AreClose(double a, double b, double tolerance = Epsilon)
        => Math.Abs(a - b) <= tolerance;

    /// <summary>Clamps <paramref name="value"/> into the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;

    public static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    public static double RadiansToDegrees(double radians) => radians * (180.0 / Math.PI);

    /// <summary>
    /// Normalizes an angle (radians) into the half-open range [0, 2π).
    /// </summary>
    public static double NormalizeAngle(double radians)
    {
        const double twoPi = Math.PI * 2.0;
        radians %= twoPi;
        return radians < 0 ? radians + twoPi : radians;
    }

    /// <summary>
    /// Rotates <paramref name="target"/> around <paramref name="origin"/> onto the nearest ray
    /// whose angle is a multiple of <paramref name="stepRadians"/>, keeping the distance from
    /// the origin. Used for the "ortho" constraint when drawing (Shift held): a step of 45°
    /// locks segments to horizontal, vertical and diagonal directions.
    /// </summary>
    public static Point2D SnapToAngleStep(Point2D origin, Point2D target, double stepRadians)
    {
        Vector2D direction = target - origin;
        double length = direction.Length;
        if (length <= Epsilon || stepRadians <= 0)
            return target;

        double angle = Math.Atan2(direction.Y, direction.X);
        double snapped = Math.Round(angle / stepRadians) * stepRadians;
        return new Point2D(origin.X + Math.Cos(snapped) * length, origin.Y + Math.Sin(snapped) * length);
    }

    /// <summary>
    /// Shortest distance from <paramref name="point"/> to the segment [<paramref name="a"/>, <paramref name="b"/>].
    /// <paramref name="closest"/> receives the nearest point on the segment.
    /// </summary>
    public static double DistancePointToSegment(Point2D point, Point2D a, Point2D b, out Point2D closest)
    {
        Vector2D ab = b - a;
        double lengthSquared = ab.LengthSquared;
        if (lengthSquared <= Epsilon)
        {
            closest = a;
            return point.DistanceTo(a);
        }

        double t = Clamp((point - a).Dot(ab) / lengthSquared, 0.0, 1.0);
        closest = a + ab * t;
        return point.DistanceTo(closest);
    }
}
