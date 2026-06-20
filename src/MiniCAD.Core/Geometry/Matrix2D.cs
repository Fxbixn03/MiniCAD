namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable 2D affine transform stored as a 3×2 matrix and applied with the
/// row-vector convention <c>[x y 1] · M</c>:
/// <code>
/// | M11 M12 0 |
/// | M21 M22 0 |
/// | OX  OY  1 |
/// </code>
/// Composition with <see cref="op_Multiply"/> applies the left operand first.
/// </summary>
public readonly struct Matrix2D : IEquatable<Matrix2D>
{
    public static readonly Matrix2D Identity = new(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

    public Matrix2D(double m11, double m12, double m21, double m22, double offsetX, double offsetY)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public double M11 { get; }
    public double M12 { get; }
    public double M21 { get; }
    public double M22 { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    public double Determinant => M11 * M22 - M12 * M21;

    /// <summary>Average absolute scale factor; useful for transforming radii uniformly.</summary>
    public double UniformScale => Math.Sqrt(Math.Abs(Determinant));

    public static Matrix2D Translation(double dx, double dy) => new(1.0, 0.0, 0.0, 1.0, dx, dy);

    public static Matrix2D Translation(Vector2D offset) => Translation(offset.X, offset.Y);

    public static Matrix2D Scaling(double scaleX, double scaleY) => new(scaleX, 0.0, 0.0, scaleY, 0.0, 0.0);

    public static Matrix2D Scaling(double scaleX, double scaleY, Point2D center)
        => Translation(-center.X, -center.Y) * Scaling(scaleX, scaleY) * Translation(center.X, center.Y);

    /// <summary>Counter-clockwise rotation by <paramref name="radians"/> about the origin.</summary>
    public static Matrix2D Rotation(double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new Matrix2D(cos, sin, -sin, cos, 0.0, 0.0);
    }

    /// <summary>Counter-clockwise rotation by <paramref name="radians"/> about <paramref name="center"/>.</summary>
    public static Matrix2D Rotation(double radians, Point2D center)
        => Translation(-center.X, -center.Y) * Rotation(radians) * Translation(center.X, center.Y);

    /// <summary>
    /// Reflection across the line through <paramref name="a"/> and <paramref name="b"/>. When the
    /// two points coincide the axis is undefined and <see cref="Identity"/> is returned. The
    /// resulting transform has determinant -1 (orientation-reversing) but is still invertible —
    /// in fact it is its own inverse — so it remains undoable.
    /// </summary>
    public static Matrix2D Reflection(Point2D a, Point2D b)
    {
        Vector2D direction = (b - a).Normalized();
        if (direction == Vector2D.Zero)
            return Identity;

        double dx = direction.X;
        double dy = direction.Y;
        var linear = new Matrix2D(dx * dx - dy * dy, 2.0 * dx * dy, 2.0 * dx * dy, dy * dy - dx * dx, 0.0, 0.0);
        return Translation(-a.X, -a.Y) * linear * Translation(a.X, a.Y);
    }

    public Point2D Transform(Point2D p)
        => new(p.X * M11 + p.Y * M21 + OffsetX, p.X * M12 + p.Y * M22 + OffsetY);

    /// <summary>Transforms a direction, ignoring the translation component.</summary>
    public Vector2D TransformVector(Vector2D v)
        => new(v.X * M11 + v.Y * M21, v.X * M12 + v.Y * M22);

    /// <summary>Returns <c>this · other</c> (apply <c>this</c> first, then <paramref name="other"/>).</summary>
    public Matrix2D Multiply(Matrix2D other) => new(
        M11 * other.M11 + M12 * other.M21,
        M11 * other.M12 + M12 * other.M22,
        M21 * other.M11 + M22 * other.M21,
        M21 * other.M12 + M22 * other.M22,
        OffsetX * other.M11 + OffsetY * other.M21 + other.OffsetX,
        OffsetX * other.M12 + OffsetY * other.M22 + other.OffsetY);

    public static Matrix2D operator *(Matrix2D a, Matrix2D b) => a.Multiply(b);

    /// <summary>Attempts to invert the matrix; returns <c>false</c> for singular transforms.</summary>
    public bool TryInvert(out Matrix2D inverse)
    {
        double det = Determinant;
        if (Math.Abs(det) <= GeometryMath.Epsilon)
        {
            inverse = Identity;
            return false;
        }

        double invDet = 1.0 / det;
        inverse = new Matrix2D(
            M22 * invDet,
            -M12 * invDet,
            -M21 * invDet,
            M11 * invDet,
            (M21 * OffsetY - M22 * OffsetX) * invDet,
            (M12 * OffsetX - M11 * OffsetY) * invDet);
        return true;
    }

    /// <summary>Inverts the matrix or throws if it is singular.</summary>
    public Matrix2D Invert()
        => TryInvert(out Matrix2D inverse)
            ? inverse
            : throw new InvalidOperationException("Matrix is not invertible.");

    public bool Equals(Matrix2D other)
        => M11.Equals(other.M11) && M12.Equals(other.M12)
        && M21.Equals(other.M21) && M22.Equals(other.M22)
        && OffsetX.Equals(other.OffsetX) && OffsetY.Equals(other.OffsetY);

    public override bool Equals(object? obj) => obj is Matrix2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(M11, M12, M21, M22, OffsetX, OffsetY);

    public static bool operator ==(Matrix2D a, Matrix2D b) => a.Equals(b);

    public static bool operator !=(Matrix2D a, Matrix2D b) => !a.Equals(b);
}
