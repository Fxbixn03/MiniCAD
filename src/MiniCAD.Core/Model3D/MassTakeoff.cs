using System.Globalization;
using System.Linq;
using System.Text;

namespace MiniCAD.Core.Model3D;

/// <summary>One aggregated row of a 3D mass take-off (#269): a material and its summed quantities.</summary>
public readonly record struct MassTakeoffRow(string Material, int Count, double VolumeM3, double MassKg);

/// <summary>
/// Aggregates 3D model volumes (and, where a material density is known, masses) by material — the
/// quantity take-off Allplan produces from a model. Mesh volumes are in model units (mm), reported
/// here in m³; mass = volume × material density (kg/m³).
/// </summary>
public static class MassTakeoff
{
    private const double MmCubedToMcubed = 1e-9;

    public static IReadOnlyList<MassTakeoffRow> Compute(IEnumerable<Model3DObject> models)
    {
        var byMaterial = new Dictionary<string, (int Count, double Vol, double Mass)>();

        foreach (Model3DObject model in models)
        {
            double volumeM3 = MeshMetrics.Compute(model.WorldMesh()).Volume * MmCubedToMcubed;
            string name = model.Material?.Name ?? model.Name ?? "Ohne Material";
            double density = model.Material?.Density ?? 0.0;

            byMaterial.TryGetValue(name, out (int Count, double Vol, double Mass) acc);
            byMaterial[name] = (acc.Count + 1, acc.Vol + volumeM3, acc.Mass + volumeM3 * density);
        }

        return byMaterial
            .Select(kv => new MassTakeoffRow(kv.Key, kv.Value.Count, kv.Value.Vol, kv.Value.Mass))
            .OrderByDescending(r => r.VolumeM3)
            .ToList();
    }

    /// <summary>The take-off as CSV (semicolon-separated, German decimal comma) for export.</summary>
    public static string ToCsv(IEnumerable<MassTakeoffRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Material;Anzahl;Volumen [m³];Masse [kg]");
        foreach (MassTakeoffRow r in rows)
            sb.AppendLine($"{r.Material};{r.Count};{F(r.VolumeM3)};{F(r.MassKg)}");
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.GetCultureInfo("de-DE"));
}
