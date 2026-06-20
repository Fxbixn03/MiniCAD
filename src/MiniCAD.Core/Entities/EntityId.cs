namespace MiniCAD.Core.Entities;

/// <summary>
/// A strongly-typed identity for an <see cref="IEntity"/>. Wrapping the GUID prevents it
/// from being confused with other identifiers (layer ids, etc.) at the type level.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public static readonly EntityId Empty = new(Guid.Empty);

    public EntityId(Guid value) => Value = value;

    public Guid Value { get; }

    public static EntityId New() => new(Guid.NewGuid());

    public bool Equals(EntityId other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);

    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);

    public override string ToString() => Value.ToString();
}
