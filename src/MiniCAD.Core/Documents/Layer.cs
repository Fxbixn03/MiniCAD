using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>
/// A named grouping of entities that carries shared display defaults. Following CAD
/// convention every document owns at least one layer named "0".
/// </summary>
public sealed class Layer
{
    public Layer(string name, StrokeStyle stroke)
        : this(Guid.NewGuid(), name, stroke)
    {
    }

    public Layer(Guid id, string name, StrokeStyle stroke)
    {
        Id = id;
        Name = name;
        Stroke = stroke;
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>The default stroke applied to entities on this layer that have no override.</summary>
    public StrokeStyle Stroke { get; set; }

    /// <summary>Active / Locked / Off — see <see cref="ElementState"/>.</summary>
    public ElementState State { get; set; } = ElementState.Active;

    /// <summary>Shown (Active or Locked).</summary>
    public bool IsVisible => State != ElementState.Off;

    /// <summary>Editable (Active only).</summary>
    public bool IsEditable => State == ElementState.Active;

    public override string ToString() => Name;
}
