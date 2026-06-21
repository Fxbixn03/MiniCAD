using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>A named numeric parameter of a parametric symbol, with its default value.</summary>
public sealed record ParametricParameterSpec(string Name, double Default);

/// <summary>
/// A catalog entry: a parametric symbol with named parameters and a generator that produces its
/// geometry (local coordinates, base point at the origin) from the current values.
/// </summary>
public sealed record ParametricSymbolDefinition(
    string Key,
    string DisplayName,
    IReadOnlyList<ParametricParameterSpec> Parameters,
    Func<IReadOnlyDictionary<string, double>, IReadOnlyList<IEntity>> Generate)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Built-in catalog of parametric symbols (Smartsymbole). Geometry is regenerated from the
/// per-instance parameter values whenever they change.
/// </summary>
public static class ParametricSymbolCatalog
{
    private static readonly Dictionary<string, ParametricSymbolDefinition> Definitions = Build();

    public static IReadOnlyList<ParametricSymbolDefinition> All => Definitions.Values.ToList();

    public static ParametricSymbolDefinition? Find(string key) => Definitions.GetValueOrDefault(key);

    /// <summary>The default parameter values for a symbol (empty if the key is unknown).</summary>
    public static Dictionary<string, double> Defaults(string key)
        => Find(key) is { } definition
            ? definition.Parameters.ToDictionary(p => p.Name, p => p.Default)
            : new Dictionary<string, double>();

    /// <summary>Generates a symbol's geometry, filling in defaults for any missing parameters.</summary>
    public static IReadOnlyList<IEntity> Generate(string key, IReadOnlyDictionary<string, double> values)
    {
        if (Find(key) is not { } definition)
            return Array.Empty<IEntity>();

        var merged = new Dictionary<string, double>();
        foreach (ParametricParameterSpec spec in definition.Parameters)
            merged[spec.Name] = values.TryGetValue(spec.Name, out double v) ? v : spec.Default;
        return definition.Generate(merged);
    }

    private static Dictionary<string, ParametricSymbolDefinition> Build()
    {
        var list = new List<ParametricSymbolDefinition>
        {
            new("rect", "Rechteck",
                new[] { new ParametricParameterSpec("Breite", 100), new ParametricParameterSpec("Höhe", 50) },
                p => new IEntity[] { Rectangle(p["Breite"], p["Höhe"]) }),

            new("window", "Fenster",
                new[] { new ParametricParameterSpec("Breite", 120), new ParametricParameterSpec("Höhe", 15) },
                p => new IEntity[]
                {
                    Rectangle(p["Breite"], p["Höhe"]),
                    new LineEntity(new Point2D(0, p["Höhe"] / 2), new Point2D(p["Breite"], p["Höhe"] / 2)),
                }),

            new("door", "Tür",
                new[] { new ParametricParameterSpec("Breite", 90) },
                p => new IEntity[]
                {
                    new LineEntity(Point2D.Origin, new Point2D(p["Breite"], 0)),     // open leaf
                    new ArcEntity(Point2D.Origin, p["Breite"], 0, Math.PI / 2),       // swing
                }),
        };

        return list.ToDictionary(d => d.Key);
    }

    private static PolylineEntity Rectangle(double width, double height)
        => PolylineEntity.Rectangle(Point2D.Origin, new Point2D(width, height));
}
