using System.Text.Json.Serialization;
using MiniCAD.Core.Documents;

namespace MiniCAD.Core.Persistence;

// Data-transfer objects describing the on-disk project format. They are deliberately kept
// separate from the domain model so the file format can stay stable while the domain evolves.

/// <summary>Root of a saved project file (<c>.mcad</c>).</summary>
public sealed class ProjectFileDto
{
    /// <summary>File-format version, for forward compatibility.</summary>
    public int Version { get; set; } = 1;

    public string Name { get; set; } = "Unbenannt";

    public DocumentDto Document { get; set; } = new();
}

public sealed class DocumentDto
{
    public List<LayerDto> Layers { get; set; } = new();

    public Guid DefaultLayerId { get; set; }

    public Guid ActiveLayerId { get; set; }

    public List<PartialDrawingDto> PartialDrawings { get; set; } = new();

    public Guid ActivePartialDrawingId { get; set; }

    /// <summary>Project-specific hatch patterns (Muster); the global library is implicit.</summary>
    public List<PatternDto> Patterns { get; set; } = new();

    /// <summary>Named text styles defined for the document.</summary>
    public List<TextStyleDto> TextStyles { get; set; } = new();

    public Guid DefaultTextStyleId { get; set; }

    public Guid ActiveTextStyleId { get; set; }

    /// <summary>Named dimension styles defined for the document.</summary>
    public List<DimStyleDto> DimStyles { get; set; } = new();

    public Guid DefaultDimStyleId { get; set; }

    public Guid ActiveDimStyleId { get; set; }

    /// <summary>Saved layer-state favorites.</summary>
    public List<LayerFavoriteDto> LayerFavorites { get; set; } = new();

    /// <summary>Block definitions (reusable symbols).</summary>
    public List<BlockDefinitionDto> BlockDefinitions { get; set; } = new();

    /// <summary>3D model-space objects.</summary>
    public List<Model3DDto> Models { get; set; } = new();

    public List<EntityDto> Entities { get; set; } = new();

    /// <summary>The user-defined origin (Nullpunkt) in absolute world coordinates.</summary>
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
}

public sealed class PatternDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "Muster";

    public ColorDto Color { get; set; } = new();

    public double LineWidth { get; set; } = 1.0;

    public List<HatchLineDto> Lines { get; set; } = new();
}

public sealed class HatchLineDto
{
    public double AngleDegrees { get; set; }
    public double Spacing { get; set; } = 10.0;
    public double Offset { get; set; }
}

public sealed class TextStyleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Standard";
    public string FontFamily { get; set; } = string.Empty;
    public double Height { get; set; } = 12.0;
    public double WidthFactor { get; set; } = 1.0;
}

public sealed class LayerFavoriteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Favorit";
    public List<LayerStateEntryDto> States { get; set; } = new();
}

public sealed class LayerStateEntryDto
{
    public Guid LayerId { get; set; }
    public ElementState State { get; set; }
}

public sealed class BlockDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Block";
    public PointDto BasePoint { get; set; } = new();
    public List<EntityDto> Entities { get; set; } = new();
    public List<string> AttributeKeys { get; set; } = new();
}

public sealed class BlockAttributeDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class DimStyleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Standard";
    public double TextHeight { get; set; } = 12.0;
    public double ArrowSize { get; set; } = 10.0;
    public double ExtensionOffset { get; set; } = 3.0;
    public double ExtensionOvershoot { get; set; } = 5.0;
    public int DecimalPlaces { get; set; }
}

public sealed class PartialDrawingDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "Teilbild 1";

    /// <summary>Active / Locked / Off.</summary>
    public ElementState? State { get; set; }

    /// <summary>Reference scale denominator (e.g. 100 for 1:100).</summary>
    public double ReferenceScale { get; set; } = 100.0;

    /// <summary>Base elevation (Z) of the Teilbild's geometry.</summary>
    public double BaseHeight { get; set; }

    /// <summary>Default extrusion/component height for the Teilbild (0 = none).</summary>
    public double Height { get; set; }

    // Legacy field (pre-state files). Read for backward compatibility, never written.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Visible { get; set; }
}

public sealed class LayerDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "0";

    public StrokeDto Stroke { get; set; } = new();

    /// <summary>Active / Locked / Off.</summary>
    public ElementState? State { get; set; }

    // Legacy fields (pre-state files). Read for backward compatibility, never written.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Visible { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Locked { get; set; }
}

public sealed class StrokeDto
{
    public ColorDto Color { get; set; } = new();

    public double Width { get; set; } = 1.0;

    /// <summary>Line type / dash pattern (Solid / Dashed / Dotted / DashDot / …).</summary>
    public string LineType { get; set; } = "Solid";

    /// <summary>Real plot line weight in mm (0 = use the pixel width).</summary>
    public double LineWeightMm { get; set; }
}

public sealed class ColorDto
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; } = 255;
}

public sealed class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class Point3DDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class Mesh3DDto
{
    public List<Point3DDto> Vertices { get; set; } = new();
    public List<int> Indices { get; set; } = new();
}

public sealed class Model3DDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Körper";
    public Mesh3DDto Mesh { get; set; } = new();

    /// <summary>The 16 row-major components of the world transform.</summary>
    public List<double> Transform { get; set; } = new();
    public ColorDto Color { get; set; } = new();
}

/// <summary>Polymorphic base for entities; the <c>kind</c> discriminator selects the shape.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LineDto), "line")]
[JsonDerivedType(typeof(CircleDto), "circle")]
[JsonDerivedType(typeof(ArcDto), "arc")]
[JsonDerivedType(typeof(PolylineDto), "polyline")]
[JsonDerivedType(typeof(PointMarkerDto), "point")]
[JsonDerivedType(typeof(EllipseDto), "ellipse")]
[JsonDerivedType(typeof(SplineDto), "spline")]
[JsonDerivedType(typeof(TextDto), "text")]
[JsonDerivedType(typeof(MTextDto), "mtext")]
[JsonDerivedType(typeof(LeaderDto), "leader")]
[JsonDerivedType(typeof(ImageDto), "image")]
[JsonDerivedType(typeof(LinearDimensionDto), "lineardim")]
[JsonDerivedType(typeof(AngularDimensionDto), "angulardim")]
[JsonDerivedType(typeof(RadialDimensionDto), "radialdim")]
[JsonDerivedType(typeof(ElevationDimensionDto), "elevationdim")]
[JsonDerivedType(typeof(OrdinateDimensionDto), "ordinatedim")]
[JsonDerivedType(typeof(BlockReferenceDto), "blockref")]
[JsonDerivedType(typeof(ParametricSymbolDto), "paramsymbol")]
[JsonDerivedType(typeof(SectionMarkDto), "sectionmark")]
[JsonDerivedType(typeof(DetailMarkDto), "detailmark")]
[JsonDerivedType(typeof(WallDto), "wall")]
[JsonDerivedType(typeof(OpeningDto), "opening")]
[JsonDerivedType(typeof(ColumnDto), "column")]
[JsonDerivedType(typeof(SlabDto), "slab")]
[JsonDerivedType(typeof(BeamDto), "beam")]
public abstract class EntityDto
{
    public Guid LayerId { get; set; }

    public Guid PartialDrawingId { get; set; }

    /// <summary>Group membership id (empty = ungrouped).</summary>
    public Guid GroupId { get; set; }

    /// <summary>Optional per-entity stroke override.</summary>
    public StrokeDto? Stroke { get; set; }

    /// <summary>True for construction geometry (Hilfskonstruktion).</summary>
    public bool IsConstruction { get; set; }
}

public sealed class LineDto : EntityDto
{
    public PointDto Start { get; set; } = new();
    public PointDto End { get; set; } = new();
}

public sealed class CircleDto : EntityDto
{
    public PointDto Center { get; set; } = new();
    public double Radius { get; set; }
}

public sealed class ArcDto : EntityDto
{
    public PointDto Center { get; set; } = new();
    public double Radius { get; set; }
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; }
}

public sealed class PolylineDto : EntityDto
{
    public List<PointDto> Points { get; set; } = new();
    public bool Closed { get; set; }

    /// <summary>Per-segment bulges (arc factor; 0 = straight). Empty/short = all straight.</summary>
    public List<double> Bulges { get; set; } = new();

    /// <summary>Id of the hatch pattern filling the region, or <c>null</c> for no fill.</summary>
    public Guid? FillPatternId { get; set; }

    /// <summary>Solid/gradient area fill of a closed region.</summary>
    public bool Filled { get; set; }
    public ColorDto FillColor { get; set; } = new();
    public ColorDto FillColor2 { get; set; } = new();
    public bool FillGradient { get; set; }
    public double FillAngle { get; set; }
}

public sealed class PointMarkerDto : EntityDto
{
    public PointDto Position { get; set; } = new();
    public double Size { get; set; } = 6.0;

    /// <summary>Marker glyph (Plus / Cross / Dot / Square).</summary>
    public string Style { get; set; } = "Plus";
}

public sealed class EllipseDto : EntityDto
{
    public PointDto Center { get; set; } = new();
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
    public double Rotation { get; set; }
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; } = Math.PI * 2.0;
}

public sealed class SplineDto : EntityDto
{
    public List<PointDto> Points { get; set; } = new();
}

public sealed class TextDto : EntityDto
{
    public PointDto Position { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public double Height { get; set; } = 12.0;
    public double Rotation { get; set; }

    /// <summary>Horizontal alignment (Left / Center / Right).</summary>
    public string HAlign { get; set; } = "Left";

    /// <summary>Vertical alignment (Top / Middle / Baseline / Bottom).</summary>
    public string VAlign { get; set; } = "Baseline";

    public Guid TextStyleId { get; set; }
    public string FontFamily { get; set; } = string.Empty;
    public double WidthFactor { get; set; } = 1.0;
}

public sealed class MTextDto : EntityDto
{
    public PointDto Position { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public double Height { get; set; } = 12.0;

    /// <summary>Wrap width; 0 wraps only on explicit newlines.</summary>
    public double Width { get; set; }
    public double Rotation { get; set; }

    /// <summary>Horizontal alignment (Left / Center / Right).</summary>
    public string HAlign { get; set; } = "Left";

    /// <summary>Vertical alignment (Top / Middle / Baseline / Bottom).</summary>
    public string VAlign { get; set; } = "Top";

    public Guid TextStyleId { get; set; }
    public string FontFamily { get; set; } = string.Empty;
    public double WidthFactor { get; set; } = 1.0;
}

public sealed class LeaderDto : EntityDto
{
    public List<PointDto> Points { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public double TextHeight { get; set; } = 12.0;
    public double ArrowSize { get; set; } = 12.0;
}

public sealed class ImageDto : EntityDto
{
    /// <summary>Base64-encoded image bytes (PNG/JPEG/…), embedded in the project.</summary>
    public string Data { get; set; } = string.Empty;
    public PointDto Origin { get; set; } = new();
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
}

/// <summary>Shared persisted fields of every dimension (style link + denormalized appearance).</summary>
public abstract class DimensionDto : EntityDto
{
    public Guid DimStyleId { get; set; }
    public double TextHeight { get; set; } = 12.0;
    public double ArrowSize { get; set; } = 10.0;
    public double ExtensionOffset { get; set; } = 3.0;
    public double ExtensionOvershoot { get; set; } = 5.0;
    public int DecimalPlaces { get; set; }
    public string? TextOverride { get; set; }
}

public sealed class LinearDimensionDto : DimensionDto
{
    public PointDto P1 { get; set; } = new();
    public PointDto P2 { get; set; } = new();
    public PointDto DimLinePoint { get; set; } = new();

    /// <summary>Aligned / Horizontal / Vertical (named to avoid the polymorphic "kind" discriminator).</summary>
    public string Orientation { get; set; } = "Aligned";
}

public sealed class AngularDimensionDto : DimensionDto
{
    public PointDto Vertex { get; set; } = new();
    public PointDto P1 { get; set; } = new();
    public PointDto P2 { get; set; } = new();
    public PointDto ArcPoint { get; set; } = new();
}

public sealed class RadialDimensionDto : DimensionDto
{
    public PointDto Center { get; set; } = new();
    public PointDto EdgePoint { get; set; } = new();
    public PointDto TextPoint { get; set; } = new();
    public bool IsDiameter { get; set; }
}

public sealed class ElevationDimensionDto : DimensionDto
{
    public PointDto Position { get; set; } = new();
    public double ZValue { get; set; }
}

public sealed class OrdinateDimensionDto : DimensionDto
{
    public PointDto Position { get; set; } = new();
    public PointDto LeaderEnd { get; set; } = new();
    public PointDto Origin { get; set; } = new();
}

public sealed class BlockReferenceDto : EntityDto
{
    public Guid DefinitionId { get; set; }
    public PointDto Position { get; set; } = new();
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; }
    public List<BlockAttributeDto> Attributes { get; set; } = new();
}

public sealed class ParametricSymbolDto : EntityDto
{
    public string SymbolKey { get; set; } = string.Empty;
    public PointDto Position { get; set; } = new();
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; }
    public List<ParameterValueDto> Parameters { get; set; } = new();
}

public sealed class ParameterValueDto
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class SectionMarkDto : EntityDto
{
    public PointDto Start { get; set; } = new();
    public PointDto End { get; set; } = new();
    public string Label { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public double Size { get; set; } = 50.0;
    public bool FlipDirection { get; set; }
}

public sealed class DetailMarkDto : EntityDto
{
    public PointDto Center { get; set; } = new();
    public double Radius { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public double TextHeight { get; set; } = 50.0;
}

public sealed class WallDto : EntityDto
{
    public PointDto Start { get; set; } = new();
    public PointDto End { get; set; } = new();
    public double Thickness { get; set; } = 240.0;
    public double Height { get; set; } = 2500.0;
    public double BaseElevation { get; set; }
}

public sealed class OpeningDto : EntityDto
{
    public PointDto Start { get; set; } = new();
    public PointDto End { get; set; } = new();
    public double Width { get; set; } = 500.0;
    public double Height { get; set; } = 2100.0;
    public double BaseElevation { get; set; }
}

public sealed class ColumnDto : EntityDto
{
    public PointDto Position { get; set; } = new();
    public bool Round { get; set; }
    public double Width { get; set; } = 300.0;
    public double Depth { get; set; } = 300.0;
    public double Height { get; set; } = 2500.0;
    public double BaseElevation { get; set; }
}

public sealed class SlabDto : EntityDto
{
    public List<PointDto> Outline { get; set; } = new();
    public double Thickness { get; set; } = 200.0;
    public double BaseElevation { get; set; }
}

public sealed class BeamDto : EntityDto
{
    public PointDto Start { get; set; } = new();
    public PointDto End { get; set; } = new();
    public double Width { get; set; } = 240.0;
    public double Height { get; set; } = 400.0;
    public double BaseElevation { get; set; } = 2500.0;
}
