namespace MiniCAD.Core.Styling;

/// <summary>
/// One family of parallel hatch lines. A pattern is built from one or more of these, which is
/// enough to express the common architectural fills (single hatch, cross-hatch, masonry, …).
/// </summary>
/// <param name="AngleDegrees">Direction of the lines, measured counter-clockwise from +X.</param>
/// <param name="Spacing">Perpendicular distance between adjacent lines, in world units.</param>
/// <param name="OffsetDegrees">Unused placeholder kept for forward compatibility.</param>
/// <param name="Offset">Shift of the whole family along its normal, in world units.</param>
public readonly record struct HatchLineDefinition(
    double AngleDegrees,
    double Spacing,
    double Offset = 0.0,
    double OffsetDegrees = 0.0);

/// <summary>
/// A named fill pattern that paints a closed region with one or more families of parallel
/// lines. Patterns are framework-agnostic: filling is computed in Core (see
/// <see cref="MiniCAD.Core.Geometry.HatchGeometry"/>) and emitted as plain line primitives,
/// so no renderer or entity has to know about hatching specifically.
/// </summary>
public sealed class HatchPattern
{
    public HatchPattern(Guid id, string name, IReadOnlyList<HatchLineDefinition> lines, Color color, double lineWidth = 1.0, bool isGlobal = false)
    {
        Id = id;
        Name = name;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        Color = color;
        LineWidth = lineWidth <= 0.0 ? 1.0 : lineWidth;
        IsGlobal = isGlobal;
    }

    public HatchPattern(string name, IReadOnlyList<HatchLineDefinition> lines, Color color, double lineWidth = 1.0, bool isGlobal = false)
        : this(Guid.NewGuid(), name, lines, color, lineWidth, isGlobal)
    {
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>The line families that make up the pattern.</summary>
    public IReadOnlyList<HatchLineDefinition> Lines { get; }

    /// <summary>The colour the hatch lines are drawn in.</summary>
    public Color Color { get; set; }

    /// <summary>Stroke width of the hatch lines, in device pixels.</summary>
    public double LineWidth { get; set; }

    /// <summary>True for patterns that come from the shared library rather than the project.</summary>
    public bool IsGlobal { get; }

    /// <summary>The stroke used to draw the hatch lines.</summary>
    public StrokeStyle Stroke => new(Color, LineWidth);

    public override string ToString() => Name;
}
