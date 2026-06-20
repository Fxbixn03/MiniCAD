namespace MiniCAD.Core.Styling;

/// <summary>
/// An immutable, framework-agnostic 8-bit-per-channel RGBA color. Kept in Core so the
/// domain and rendering abstraction never depend on Avalonia or SkiaSharp color types.
/// </summary>
public readonly struct Color : IEquatable<Color>
{
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>Packs the color as <c>0xAARRGGBB</c>.</summary>
    public uint ToArgb() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Linearly blends toward <paramref name="other"/> by <paramref name="t"/> (0 = this, 1 = other).</summary>
    public Color Lerp(Color other, double t)
    {
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        return new Color(
            Channel(R, other.R, t),
            Channel(G, other.G, t),
            Channel(B, other.B, t),
            Channel(A, other.A, t));
    }

    private static byte Channel(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Red = new(220, 50, 47);
    public static readonly Color Green = new(133, 153, 0);
    public static readonly Color Blue = new(38, 139, 210);
    public static readonly Color Yellow = new(181, 137, 0);
    public static readonly Color Cyan = new(42, 161, 152);
    public static readonly Color Magenta = new(211, 54, 130);
    public static readonly Color Gray = new(128, 128, 128);
    public static readonly Color LightGray = new(200, 200, 200);
    public static readonly Color DarkGray = new(64, 64, 64);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(Color a, Color b) => a.Equals(b);

    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
