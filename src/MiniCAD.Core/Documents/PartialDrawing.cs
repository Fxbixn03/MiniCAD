namespace MiniCAD.Core.Documents;

/// <summary>
/// A Teilbild (partial drawing) — an Allplan-style overlay/sheet that groups entities and can
/// be shown or hidden independently. Entities reference their Teilbild by id; the order of
/// partial drawings in the document defines their stacking ("Ebene").
/// </summary>
public sealed class PartialDrawing
{
    public PartialDrawing(string name)
        : this(Guid.NewGuid(), name)
    {
    }

    public PartialDrawing(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>Active / Locked / Off — see <see cref="ElementState"/>.</summary>
    public ElementState State { get; set; } = ElementState.Active;

    /// <summary>
    /// The Teilbild's reference scale denominator (e.g. 100 for 1:100). Scale-dependent display
    /// (hatch density, and later text/dimensions) is sized relative to a 1:100 base, so a smaller
    /// denominator renders denser. Always positive.
    /// </summary>
    private double _referenceScale = 100.0;

    public double ReferenceScale
    {
        get => _referenceScale;
        set => _referenceScale = value > 0 ? value : 100.0;
    }

    /// <summary>Hatch/annotation size factor relative to the 1:100 authoring base.</summary>
    public double ModelScaleFactor => ReferenceScale / 100.0;

    /// <summary>Shown (Active or Locked).</summary>
    public bool IsVisible => State != ElementState.Off;

    /// <summary>Editable (Active only).</summary>
    public bool IsEditable => State == ElementState.Active;

    public override string ToString() => Name;
}
