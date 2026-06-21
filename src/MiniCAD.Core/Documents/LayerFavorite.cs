namespace MiniCAD.Core.Documents;

/// <summary>
/// A named, saveable snapshot of every layer's state (Active/Locked/Off) — an Allplan-style
/// layer favorite ("Druckstift"/visibility set). Applying it restores the captured states for
/// the layers that still exist.
/// </summary>
public sealed class LayerFavorite
{
    public LayerFavorite(string name)
        : this(Guid.NewGuid(), name, new Dictionary<Guid, ElementState>())
    {
    }

    public LayerFavorite(Guid id, string name, IDictionary<Guid, ElementState> states)
    {
        Id = id;
        Name = name;
        States = new Dictionary<Guid, ElementState>(states);
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>The captured state per layer id.</summary>
    public Dictionary<Guid, ElementState> States { get; }

    public override string ToString() => Name;
}
