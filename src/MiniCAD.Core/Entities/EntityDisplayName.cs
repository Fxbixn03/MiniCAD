namespace MiniCAD.Core.Entities;

/// <summary>
/// Maps entity types to short, human-readable German labels for reports and palettes
/// (document statistics, purge reports, the object inspector). Centralized so every report
/// uses the same wording; unknown types fall back to a cleaned-up type name.
/// </summary>
public static class EntityDisplayName
{
    public static string Of(IEntity entity) => entity switch
    {
        LineEntity => "Linie",
        PolylineEntity => "Polylinie",
        CircleEntity => "Kreis",
        ArcEntity => "Bogen",
        EllipseEntity => "Ellipse",
        SplineEntity => "Spline",
        PointEntity => "Punkt",
        TextEntity => "Text",
        MTextEntity => "Mehrzeiliger Text",
        LeaderEntity => "Führungslinie",
        LinearDimensionEntity => "Längenmaß",
        AngularDimensionEntity => "Winkelmaß",
        RadialDimensionEntity => "Radialmaß",
        ElevationDimensionEntity => "Höhenkote",
        OrdinateDimensionEntity => "Koordinatenmaß",
        BlockReferenceEntity => "Blockreferenz",
        ParametricSymbolEntity => "Smartsymbol",
        ImageEntity => "Bild",
        WallEntity => "Wand",
        OpeningEntity => "Aussparung",
        ColumnEntity => "Stütze",
        SlabEntity => "Decke",
        BeamEntity => "Träger",
        SectionMarkEntity => "Schnittzeichen",
        DetailMarkEntity => "Detailzeichen",
        _ => CleanTypeName(entity.GetType().Name),
    };

    private static string CleanTypeName(string typeName)
        => typeName.EndsWith("Entity", StringComparison.Ordinal)
            ? typeName[..^"Entity".Length]
            : typeName;
}
