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

public sealed class PartialDrawingDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "Teilbild 1";

    /// <summary>Active / Locked / Off.</summary>
    public ElementState? State { get; set; }

    /// <summary>Reference scale denominator (e.g. 100 for 1:100).</summary>
    public double ReferenceScale { get; set; } = 100.0;

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
public abstract class EntityDto
{
    public Guid LayerId { get; set; }

    public Guid PartialDrawingId { get; set; }

    /// <summary>Optional per-entity stroke override.</summary>
    public StrokeDto? Stroke { get; set; }
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

    /// <summary>Id of the hatch pattern filling the region, or <c>null</c> for no fill.</summary>
    public Guid? FillPatternId { get; set; }
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
}
