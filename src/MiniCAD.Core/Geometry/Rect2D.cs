namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable axis-aligned bounding box in world coordinates. The bounds are stored
/// as min/max corners and are assumed to be normalized (min ≤ max on both axes); use the
/// <c>From*</c> factory methods to build one from arbitrary input.
/// </summary>
public readonly struct Rect2D : IEquatable<Rect2D>
{
    public Rect2D(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    public Point2D Min => new(MinX, MinY);
    public Point2D Max => new(MaxX, MaxY);
    public Point2D Center => new((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5);

    /// <summary>Builds a normalized box spanning the two corner points.</summary>
    public static Rect2D FromPoints(Point2D a, Point2D b) => new(
        Math.Min(a.X, b.X),
        Math.Min(a.Y, b.Y),
        Math.Max(a.X, b.X),
        Math.Max(a.Y, b.Y));

    public static Rect2D FromCenter(Point2D center, double halfWidth, double halfHeight) => new(
        center.X - halfWidth,
        center.Y - halfHeight,
        center.X + halfWidth,
        center.Y + halfHeight);

    public bool Contains(Point2D p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

    public bool Intersects(Rect2D other)
        => MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;

    /// <summary>The smallest box containing both this box and <paramref name="other"/>.</summary>
    public Rect2D Union(Rect2D other) => new(
        Math.Min(MinX, other.MinX),
        Math.Min(MinY, other.MinY),
        Math.Max(MaxX, other.MaxX),
        Math.Max(MaxY, other.MaxY));

    /// <summary>The smallest box containing this box and <paramref name="point"/>.</summary>
    public Rect2D Union(Point2D point) => new(
        Math.Min(MinX, point.X),
        Math.Min(MinY, point.Y),
        Math.Max(MaxX, point.X),
        Math.Max(MaxY, point.Y));

    /// <summary>Grows (or, with a negative value, shrinks) the box by <paramref name="amount"/> on every side.</summary>
    public Rect2D Inflate(double amount) => new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

    public bool Equals(Rect2D other)
        => MinX.Equals(other.MinX) && MinY.Equals(other.MinY)
        && MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);

    public override bool Equals(object? obj) => obj is Rect2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);

    public static bool operator ==(Rect2D a, Rect2D b) => a.Equals(b);

    public static bool operator !=(Rect2D a, Rect2D b) => !a.Equals(b);

    public override string ToString() => $"[{Min} → {Max}]";
}
