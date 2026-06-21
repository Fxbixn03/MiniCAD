namespace MiniCAD.Core.Geometry;

/// <summary>An axis-aligned 3D bounding box (the 3D counterpart to <see cref="Rect2D"/>).</summary>
public readonly struct BoundingBox3D : IEquatable<BoundingBox3D>
{
    public BoundingBox3D(Point3D min, Point3D max)
    {
        Min = min;
        Max = max;
    }

    public Point3D Min { get; }
    public Point3D Max { get; }

    public Point3D Center => Min.Lerp(Max, 0.5);

    public Vector3D Size => Max - Min;

    public static BoundingBox3D FromPoints(IEnumerable<Point3D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool any = false;
        foreach (Point3D p in points)
        {
            any = true;
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
        }

        return any
            ? new BoundingBox3D(new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ))
            : new BoundingBox3D(Point3D.Origin, Point3D.Origin);
    }

    public BoundingBox3D Union(Point3D p) => new(
        new Point3D(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z)),
        new Point3D(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z)));

    public BoundingBox3D Union(BoundingBox3D other) => Union(other.Min).Union(other.Max);

    public bool Contains(Point3D p)
        => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    /// <summary>The eight corners of the box (for projecting/framing).</summary>
    public IEnumerable<Point3D> Corners()
    {
        yield return new Point3D(Min.X, Min.Y, Min.Z);
        yield return new Point3D(Max.X, Min.Y, Min.Z);
        yield return new Point3D(Max.X, Max.Y, Min.Z);
        yield return new Point3D(Min.X, Max.Y, Min.Z);
        yield return new Point3D(Min.X, Min.Y, Max.Z);
        yield return new Point3D(Max.X, Min.Y, Max.Z);
        yield return new Point3D(Max.X, Max.Y, Max.Z);
        yield return new Point3D(Min.X, Max.Y, Max.Z);
    }

    public bool Equals(BoundingBox3D other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    public override bool Equals(object? obj) => obj is BoundingBox3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Min, Max);

    public static bool operator ==(BoundingBox3D a, BoundingBox3D b) => a.Equals(b);

    public static bool operator !=(BoundingBox3D a, BoundingBox3D b) => !a.Equals(b);
}
