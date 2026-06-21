using System.IO;
using System.Linq;
using System.Text.Json;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Materials;

/// <summary>
/// A named collection of building materials (#276/#277). Ships a <see cref="Default"/> library of
/// typical construction materials and round-trips to the JSON-based <c>.mcmat</c> format.
/// </summary>
public sealed class MaterialLibrary
{
    /// <summary>File extension for material libraries (MiniCAD Material).</summary>
    public const string FileExtension = ".mcmat";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly List<MaterialDefinition> _materials = new();

    public MaterialLibrary()
    {
    }

    public MaterialLibrary(IEnumerable<MaterialDefinition> materials) => _materials.AddRange(materials);

    public IReadOnlyList<MaterialDefinition> Materials => _materials;

    /// <summary>Adds or replaces (by name, case-insensitive) a material.</summary>
    public void Add(MaterialDefinition material)
    {
        _materials.RemoveAll(m => string.Equals(m.Name, material.Name, System.StringComparison.OrdinalIgnoreCase));
        _materials.Add(material);
    }

    public bool TryGet(string name, out MaterialDefinition material)
    {
        MaterialDefinition? found = _materials.FirstOrDefault(
            m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase));
        material = found ?? Concrete;
        return found is not null;
    }

    public IEnumerable<MaterialDefinition> GetByCategory(MaterialCategory category)
        => _materials.Where(m => m.Category == category);

    // ----- Persistence (.mcmat) -----

    public string ToJson() => JsonSerializer.Serialize(_materials, JsonOptions);

    public static MaterialLibrary FromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<MaterialDefinition>>(json);
        return new MaterialLibrary(dtos ?? Enumerable.Empty<MaterialDefinition>());
    }

    public void Save(string path) => File.WriteAllText(path, ToJson());

    public static MaterialLibrary Load(string path) => FromJson(File.ReadAllText(path));

    // ----- Predefined building materials -----

    public static MaterialDefinition Concrete { get; } =
        new("Stahlbeton", MaterialCategory.Concrete, Hex(0x78, 0x78, 0x78), 0.35f, 0.08f, 12f, 1f, 2500);

    /// <summary>A fresh copy of the default building-material library.</summary>
    public static MaterialLibrary Default => new(new[]
    {
        Concrete,
        new MaterialDefinition("Sichtbeton", MaterialCategory.Concrete, Hex(0x9A, 0x9A, 0x9A), 0.35f, 0.12f, 20f, 1f, 2400),
        new MaterialDefinition("Vollziegel", MaterialCategory.Masonry, Hex(0xA0, 0x52, 0x2D), 0.4f, 0.05f, 8f, 1f, 1800),
        new MaterialDefinition("Kalksandstein", MaterialCategory.Masonry, Hex(0xD4, 0xC5, 0xA9), 0.4f, 0.06f, 10f, 1f, 2000),
        new MaterialDefinition("Nadelholz", MaterialCategory.Wood, Hex(0xC6, 0x86, 0x42), 0.35f, 0.06f, 10f, 1f, 500),
        new MaterialDefinition("Baustahl", MaterialCategory.Steel, Hex(0x70, 0x80, 0x90), 0.25f, 0.6f, 64f, 1f, 7850),
        new MaterialDefinition("Floatglas", MaterialCategory.Glass, Hex(0xB0, 0xC4, 0xDE), 0.2f, 0.8f, 96f, 0.35f, 2500),
        new MaterialDefinition("Mineralwolle", MaterialCategory.Insulation, Hex(0xF4, 0xD0, 0x3F), 0.45f, 0.02f, 4f, 1f, 50),
        new MaterialDefinition("Erdreich", MaterialCategory.Earth, Hex(0x6B, 0x44, 0x23), 0.45f, 0.02f, 4f, 1f, 1800),
        new MaterialDefinition("Standard", MaterialCategory.Generic, Hex(0xB4, 0xB4, 0xB4), 0.3f, 0.1f, 16f, 1f, 0),
    });

    private static Color Hex(byte r, byte g, byte b) => new(r, g, b);
}
