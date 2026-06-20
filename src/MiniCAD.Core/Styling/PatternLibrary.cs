namespace MiniCAD.Core.Styling;

/// <summary>
/// The global, always-available catalogue of hatch patterns. These are the "global" patterns
/// from the user's point of view; a project can additionally carry its own patterns on the
/// document. The ids are fixed so a fill that references a built-in keeps resolving across
/// sessions and machines.
/// </summary>
public static class PatternLibrary
{
    private static readonly Color HatchColor = new(190, 190, 196);

    /// <summary>45° single hatch — the everyday "Schraffur".</summary>
    public static HatchPattern Diagonal { get; } = new(
        new Guid("11111111-0000-0000-0000-000000000001"),
        "Schraffur 45°",
        new[] { new HatchLineDefinition(45, 40) },
        HatchColor,
        isGlobal: true);

    /// <summary>Cross hatch at ±45°.</summary>
    public static HatchPattern Cross { get; } = new(
        new Guid("11111111-0000-0000-0000-000000000002"),
        "Kreuzschraffur",
        new[] { new HatchLineDefinition(45, 40), new HatchLineDefinition(135, 40) },
        HatchColor,
        isGlobal: true);

    /// <summary>Masonry / brickwork — a tighter 45° hatch as used for "Mauerwerk".</summary>
    public static HatchPattern Masonry { get; } = new(
        new Guid("11111111-0000-0000-0000-000000000003"),
        "Mauerwerk",
        new[] { new HatchLineDefinition(45, 18) },
        HatchColor,
        isGlobal: true);

    /// <summary>Reinforced concrete — sparse cross hatch.</summary>
    public static HatchPattern Concrete { get; } = new(
        new Guid("11111111-0000-0000-0000-000000000004"),
        "Beton",
        new[] { new HatchLineDefinition(45, 70), new HatchLineDefinition(135, 70) },
        HatchColor,
        isGlobal: true);

    /// <summary>Horizontal lining (e.g. earth / fill).</summary>
    public static HatchPattern Horizontal { get; } = new(
        new Guid("11111111-0000-0000-0000-000000000005"),
        "Linien waagerecht",
        new[] { new HatchLineDefinition(0, 30) },
        HatchColor,
        isGlobal: true);

    /// <summary>All built-in patterns, in display order.</summary>
    public static IReadOnlyList<HatchPattern> All { get; } = new[]
    {
        Diagonal, Cross, Masonry, Concrete, Horizontal,
    };

    /// <summary>Returns the built-in pattern with the given id, or <c>null</c>.</summary>
    public static HatchPattern? Find(Guid id)
    {
        foreach (HatchPattern pattern in All)
        {
            if (pattern.Id == id)
                return pattern;
        }

        return null;
    }
}
