namespace MiniCAD.Core.Geometry;

/// <summary>An immutable unit quaternion for 3D rotations (double precision).</summary>
public readonly struct Quaternion : IEquatable<Quaternion>
{
    public static readonly Quaternion Identity = new(0.0, 0.0, 0.0, 1.0);

    public Quaternion(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public double W { get; }

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

    public Quaternion Normalized()
    {
        double length = Length;
        return length <= GeometryMath.Epsilon ? Identity : new Quaternion(X / length, Y / length, Z / length, W / length);
    }

    /// <summary>A rotation of <paramref name="angle"/> radians about <paramref name="axis"/>.</summary>
    public static Quaternion FromAxisAngle(Vector3D axis, double angle)
    {
        Vector3D n = axis.Normalized();
        double half = angle * 0.5;
        double s = Math.Sin(half);
        return new Quaternion(n.X * s, n.Y * s, n.Z * s, Math.Cos(half));
    }

    /// <summary>Hamilton product: applies <paramref name="b"/> then <paramref name="a"/>.</summary>
    public static Quaternion operator *(Quaternion a, Quaternion b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    /// <summary>Rotates a vector by this quaternion.</summary>
    public Vector3D Rotate(Vector3D v)
    {
        var u = new Vector3D(X, Y, Z);
        double s = W;
        return 2.0 * u.Dot(v) * u + (s * s - u.Dot(u)) * v + 2.0 * s * u.Cross(v);
    }

    public bool Equals(Quaternion other)
        => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    public override bool Equals(object? obj) => obj is Quaternion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public static bool operator ==(Quaternion a, Quaternion b) => a.Equals(b);

    public static bool operator !=(Quaternion a, Quaternion b) => !a.Equals(b);

    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###}, {W:0.###})";
}
