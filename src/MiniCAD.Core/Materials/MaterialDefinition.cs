using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Materials;

/// <summary>
/// An immutable building material (#274): a name, a category, a diffuse colour and simplified
/// Phong shading factors (ambient/specular/shininess/opacity), plus an optional texture path for
/// later. Framework-agnostic — no Skia/Avalonia. The shaded 3D renderer reads the colour and
/// factors; the density (kg/m³) feeds mass take-offs.
/// </summary>
public record MaterialDefinition(
    string Name,
    MaterialCategory Category,
    Color DiffuseColor,
    float AmbientFactor = 0.3f,
    float SpecularFactor = 0.1f,
    float Shininess = 16f,
    float Opacity = 1.0f,
    double Density = 0.0,
    string? TexturePath = null);
