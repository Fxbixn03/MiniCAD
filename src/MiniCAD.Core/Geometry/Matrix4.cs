namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable 4×4 affine/projection matrix (double precision) using the same row-vector
/// convention as <see cref="Matrix2D"/>: points are transformed as <c>[x y z 1] · M</c>, the
/// translation lives in the fourth row, and <c>a * b</c> applies <c>a</c> first. Projection
/// helpers follow the DirectX left-handed conventions (also row-vector).
/// </summary>
public readonly struct Matrix4 : IEquatable<Matrix4>
{
    public static readonly Matrix4 Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public Matrix4(
        double m11, double m12, double m13, double m14,
        double m21, double m22, double m23, double m24,
        double m31, double m32, double m33, double m34,
        double m41, double m42, double m43, double m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    public double M11 { get; } public double M12 { get; } public double M13 { get; } public double M14 { get; }
    public double M21 { get; } public double M22 { get; } public double M23 { get; } public double M24 { get; }
    public double M31 { get; } public double M32 { get; } public double M33 { get; } public double M34 { get; }
    public double M41 { get; } public double M42 { get; } public double M43 { get; } public double M44 { get; }

    public static Matrix4 Translation(Vector3D t) => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        t.X, t.Y, t.Z, 1);

    public static Matrix4 Scaling(double s) => Scaling(s, s, s);

    public static Matrix4 Scaling(double sx, double sy, double sz) => new(
        sx, 0, 0, 0,
        0, sy, 0, 0,
        0, 0, sz, 0,
        0, 0, 0, 1);

    public static Matrix4 RotationX(double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new(1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1);
    }

    public static Matrix4 RotationY(double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new(c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1);
    }

    public static Matrix4 RotationZ(double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new(c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
    }

    public static Matrix4 CreateFromQuaternion(Quaternion q)
    {
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        double xx = x * x, yy = y * y, zz = z * z;
        double xy = x * y, xz = x * z, yz = y * z, wx = w * x, wy = w * y, wz = w * z;
        return new(
            1 - 2 * (yy + zz), 2 * (xy + wz), 2 * (xz - wy), 0,
            2 * (xy - wz), 1 - 2 * (xx + zz), 2 * (yz + wx), 0,
            2 * (xz + wy), 2 * (yz - wx), 1 - 2 * (xx + yy), 0,
            0, 0, 0, 1);
    }

    /// <summary>Left-handed view matrix looking from <paramref name="eye"/> toward <paramref name="target"/>.</summary>
    public static Matrix4 CreateLookAt(Point3D eye, Point3D target, Vector3D up)
    {
        Vector3D zaxis = (target - eye).Normalized();
        Vector3D xaxis = up.Cross(zaxis).Normalized();
        Vector3D yaxis = zaxis.Cross(xaxis);
        Vector3D e = eye.ToVector3D();
        return new(
            xaxis.X, yaxis.X, zaxis.X, 0,
            xaxis.Y, yaxis.Y, zaxis.Y, 0,
            xaxis.Z, yaxis.Z, zaxis.Z, 0,
            -xaxis.Dot(e), -yaxis.Dot(e), -zaxis.Dot(e), 1);
    }

    /// <summary>Left-handed perspective projection (clip z in [0,1]).</summary>
    public static Matrix4 CreatePerspective(double fovY, double aspect, double near, double far)
    {
        double yScale = 1.0 / Math.Tan(fovY * 0.5);
        double xScale = yScale / aspect;
        return new(
            xScale, 0, 0, 0,
            0, yScale, 0, 0,
            0, 0, far / (far - near), 1,
            0, 0, -near * far / (far - near), 0);
    }

    /// <summary>Left-handed orthographic projection of the given view-space extent.</summary>
    public static Matrix4 CreateOrthographic(double width, double height, double near, double far) => new(
        2.0 / width, 0, 0, 0,
        0, 2.0 / height, 0, 0,
        0, 0, 1.0 / (far - near), 0,
        0, 0, -near / (far - near), 1);

    /// <summary>Transforms a point as an affine row vector (assumes the matrix is affine).</summary>
    public Point3D Transform(Point3D p) => new(
        p.X * M11 + p.Y * M21 + p.Z * M31 + M41,
        p.X * M12 + p.Y * M22 + p.Z * M32 + M42,
        p.X * M13 + p.Y * M23 + p.Z * M33 + M43);

    /// <summary>Transforms a direction (ignores translation).</summary>
    public Vector3D TransformVector(Vector3D v) => new(
        v.X * M11 + v.Y * M21 + v.Z * M31,
        v.X * M12 + v.Y * M22 + v.Z * M32,
        v.X * M13 + v.Y * M23 + v.Z * M33);

    /// <summary>Full homogeneous transform; <paramref name="w"/> receives the projected w for the perspective divide.</summary>
    public Point3D TransformWithW(Point3D p, out double w)
    {
        double x = p.X * M11 + p.Y * M21 + p.Z * M31 + M41;
        double y = p.X * M12 + p.Y * M22 + p.Z * M32 + M42;
        double z = p.X * M13 + p.Y * M23 + p.Z * M33 + M43;
        w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;
        return new Point3D(x, y, z);
    }

    public Matrix4 Multiply(in Matrix4 b)
    {
        return new(
            M11 * b.M11 + M12 * b.M21 + M13 * b.M31 + M14 * b.M41,
            M11 * b.M12 + M12 * b.M22 + M13 * b.M32 + M14 * b.M42,
            M11 * b.M13 + M12 * b.M23 + M13 * b.M33 + M14 * b.M43,
            M11 * b.M14 + M12 * b.M24 + M13 * b.M34 + M14 * b.M44,

            M21 * b.M11 + M22 * b.M21 + M23 * b.M31 + M24 * b.M41,
            M21 * b.M12 + M22 * b.M22 + M23 * b.M32 + M24 * b.M42,
            M21 * b.M13 + M22 * b.M23 + M23 * b.M33 + M24 * b.M43,
            M21 * b.M14 + M22 * b.M24 + M23 * b.M34 + M24 * b.M44,

            M31 * b.M11 + M32 * b.M21 + M33 * b.M31 + M34 * b.M41,
            M31 * b.M12 + M32 * b.M22 + M33 * b.M32 + M34 * b.M42,
            M31 * b.M13 + M32 * b.M23 + M33 * b.M33 + M34 * b.M43,
            M31 * b.M14 + M32 * b.M24 + M33 * b.M34 + M34 * b.M44,

            M41 * b.M11 + M42 * b.M21 + M43 * b.M31 + M44 * b.M41,
            M41 * b.M12 + M42 * b.M22 + M43 * b.M32 + M44 * b.M42,
            M41 * b.M13 + M42 * b.M23 + M43 * b.M33 + M44 * b.M43,
            M41 * b.M14 + M42 * b.M24 + M43 * b.M34 + M44 * b.M44);
    }

    public static Matrix4 operator *(Matrix4 a, Matrix4 b) => a.Multiply(b);

    /// <summary>Inverts the matrix via Gauss-Jordan elimination; returns <c>false</c> if singular.</summary>
    public bool TryInvert(out Matrix4 inverse)
    {
        double[,] a =
        {
            { M11, M12, M13, M14 },
            { M21, M22, M23, M24 },
            { M31, M32, M33, M34 },
            { M41, M42, M43, M44 },
        };
        double[,] r =
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 },
        };

        for (int col = 0; col < 4; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < 4; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                    pivot = row;
            }

            if (Math.Abs(a[pivot, col]) <= GeometryMath.Epsilon)
            {
                inverse = Identity;
                return false;
            }

            SwapRows(a, col, pivot);
            SwapRows(r, col, pivot);

            double diag = a[col, col];
            for (int k = 0; k < 4; k++)
            {
                a[col, k] /= diag;
                r[col, k] /= diag;
            }

            for (int row = 0; row < 4; row++)
            {
                if (row == col)
                    continue;
                double factor = a[row, col];
                for (int k = 0; k < 4; k++)
                {
                    a[row, k] -= factor * a[col, k];
                    r[row, k] -= factor * r[col, k];
                }
            }
        }

        inverse = new Matrix4(
            r[0, 0], r[0, 1], r[0, 2], r[0, 3],
            r[1, 0], r[1, 1], r[1, 2], r[1, 3],
            r[2, 0], r[2, 1], r[2, 2], r[2, 3],
            r[3, 0], r[3, 1], r[3, 2], r[3, 3]);
        return true;
    }

    private static void SwapRows(double[,] m, int a, int b)
    {
        if (a == b)
            return;
        for (int k = 0; k < 4; k++)
            (m[a, k], m[b, k]) = (m[b, k], m[a, k]);
    }

    public bool Equals(Matrix4 other)
        => M11.Equals(other.M11) && M12.Equals(other.M12) && M13.Equals(other.M13) && M14.Equals(other.M14)
        && M21.Equals(other.M21) && M22.Equals(other.M22) && M23.Equals(other.M23) && M24.Equals(other.M24)
        && M31.Equals(other.M31) && M32.Equals(other.M32) && M33.Equals(other.M33) && M34.Equals(other.M34)
        && M41.Equals(other.M41) && M42.Equals(other.M42) && M43.Equals(other.M43) && M44.Equals(other.M44);

    public override bool Equals(object? obj) => obj is Matrix4 other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(M11); hash.Add(M12); hash.Add(M13); hash.Add(M14);
        hash.Add(M21); hash.Add(M22); hash.Add(M23); hash.Add(M24);
        hash.Add(M31); hash.Add(M32); hash.Add(M33); hash.Add(M34);
        hash.Add(M41); hash.Add(M42); hash.Add(M43); hash.Add(M44);
        return hash.ToHashCode();
    }

    public static bool operator ==(Matrix4 a, Matrix4 b) => a.Equals(b);

    public static bool operator !=(Matrix4 a, Matrix4 b) => !a.Equals(b);
}
