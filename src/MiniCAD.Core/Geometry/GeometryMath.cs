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

    /// <summary>
    /// True if the segments [<paramref name="a"/>, <paramref name="b"/>] and
    /// [<paramref name="c"/>, <paramref name="d"/>] cross or touch. Collinear overlap is treated
    /// conservatively as an intersection.
    /// </summary>
    public static bool SegmentsIntersect(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        Vector2D ab = b - a;
        Vector2D cd = d - c;
        double denominator = ab.Cross(cd);

        if (Math.Abs(denominator) <= Epsilon)
        {
            // Parallel: count it as touching only when collinear and the projections overlap.
            if (Math.Abs((c - a).Cross(ab)) > Epsilon)
                return false;

            double abLengthSquared = ab.LengthSquared;
            if (abLengthSquared <= Epsilon)
                return DistancePointToSegment(a, c, d, out _) <= Epsilon;

            double tc = (c - a).Dot(ab) / abLengthSquared;
            double td = (d - a).Dot(ab) / abLengthSquared;
            double lo = Math.Min(tc, td);
            double hi = Math.Max(tc, td);
            return hi >= 0.0 && lo <= 1.0;
        }

        double t = (c - a).Cross(cd) / denominator;
        double u = (c - a).Cross(ab) / denominator;
        return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
    }

    /// <summary>
    /// True if the segment [<paramref name="a"/>, <paramref name="b"/>] touches the axis-aligned
    /// <paramref name="rect"/> — either an endpoint lies inside it or the segment crosses an edge.
    /// </summary>
    public static bool SegmentIntersectsRect(Point2D a, Point2D b, Rect2D rect)
    {
        if (rect.Contains(a) || rect.Contains(b))
            return true;

        var bottomLeft = new Point2D(rect.MinX, rect.MinY);
        var bottomRight = new Point2D(rect.MaxX, rect.MinY);
        var topRight = new Point2D(rect.MaxX, rect.MaxY);
        var topLeft = new Point2D(rect.MinX, rect.MaxY);

        return SegmentsIntersect(a, b, bottomLeft, bottomRight)
            || SegmentsIntersect(a, b, bottomRight, topRight)
            || SegmentsIntersect(a, b, topRight, topLeft)
            || SegmentsIntersect(a, b, topLeft, bottomLeft);
    }

    /// <summary>
    /// Intersection of the two infinite lines through (<paramref name="a1"/>, <paramref name="a2"/>)
    /// and (<paramref name="b1"/>, <paramref name="b2"/>). Returns <c>false</c> when the lines are
    /// parallel (or a defining pair is degenerate).
    /// </summary>
    public static bool TryLineIntersection(Point2D a1, Point2D a2, Point2D b1, Point2D b2, out Point2D intersection)
    {
        Vector2D r = a2 - a1;
        Vector2D s = b2 - b1;
        double denominator = r.Cross(s);
        if (Math.Abs(denominator) <= Epsilon)
        {
            intersection = default;
            return false;
        }

        double t = (b1 - a1).Cross(s) / denominator;
        intersection = a1 + r * t;
        return true;
    }

    /// <summary>
    /// Intersection of the two segments [<paramref name="a"/>, <paramref name="b"/>] and
    /// [<paramref name="c"/>, <paramref name="d"/>] if they actually cross within both spans.
    /// Parallel/collinear segments report no single point.
    /// </summary>
    public static bool IntersectSegments(Point2D a, Point2D b, Point2D c, Point2D d, out Point2D point)
    {
        Vector2D r = b - a;
        Vector2D s = d - c;
        double denominator = r.Cross(s);
        if (Math.Abs(denominator) <= Epsilon)
        {
            point = default;
            return false;
        }

        double t = (c - a).Cross(s) / denominator;
        double u = (c - a).Cross(r) / denominator;
        if (t < -Epsilon || t > 1 + Epsilon || u < -Epsilon || u > 1 + Epsilon)
        {
            point = default;
            return false;
        }

        point = a + r * t;
        return true;
    }

    /// <summary>Adds the 0–2 points where the segment [<paramref name="a"/>, <paramref name="b"/>] meets the circle.</summary>
    public static void IntersectSegmentCircle(Point2D a, Point2D b, Point2D center, double radius, ICollection<Point2D> results)
    {
        Vector2D d = b - a;
        double aa = d.Dot(d);
        if (aa <= Epsilon)
            return;

        Vector2D f = a - center;
        double bb = 2.0 * f.Dot(d);
        double cc = f.Dot(f) - radius * radius;
        double discriminant = bb * bb - 4.0 * aa * cc;
        if (discriminant < 0)
            return;

        double root = Math.Sqrt(discriminant);
        foreach (double t in stackalloc[] { (-bb - root) / (2.0 * aa), (-bb + root) / (2.0 * aa) })
        {
            if (t >= -Epsilon && t <= 1 + Epsilon)
                results.Add(a + d * t);
        }
    }

    /// <summary>Adds the 0–2 points where two circles meet (none for coincident/contained circles).</summary>
    public static void IntersectCircles(Point2D c1, double r1, Point2D c2, double r2, ICollection<Point2D> results)
    {
        double distance = c1.DistanceTo(c2);
        if (distance <= Epsilon || distance > r1 + r2 + Epsilon || distance < Math.Abs(r1 - r2) - Epsilon)
            return;

        double a = (r1 * r1 - r2 * r2 + distance * distance) / (2.0 * distance);
        double hSquared = r1 * r1 - a * a;
        double h = hSquared <= 0 ? 0 : Math.Sqrt(hSquared);
        Vector2D direction = (c2 - c1) / distance;
        Point2D midpoint = c1 + direction * a;
        Vector2D offset = direction.Perpendicular() * h;

        results.Add(midpoint + offset);
        if (h > Epsilon)
            results.Add(midpoint - offset);
    }

    /// <summary>
    /// True if the absolute <paramref name="angle"/> lies within the arc that starts at
    /// <paramref name="startAngle"/> and spans <paramref name="sweepAngle"/> (signed, radians).
    /// </summary>
    public static bool AngleInSweep(double startAngle, double sweepAngle, double angle)
    {
        if (sweepAngle >= 0)
            return NormalizeAngle(angle - startAngle) <= sweepAngle + Epsilon;

        return NormalizeAngle(startAngle - angle) <= -sweepAngle + Epsilon;
    }
}
