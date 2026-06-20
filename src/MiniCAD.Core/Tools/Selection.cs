using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Tools;

/// <summary>
/// The current set of selected entities. This is editing/session state — deliberately not
/// part of <see cref="Documents.CadDocument"/>, which models the drawing itself.
/// </summary>
public sealed class Selection
{
    private readonly List<IEntity> _items = new();

    public IReadOnlyList<IEntity> Items => _items;

    public int Count => _items.Count;

    public bool IsEmpty => _items.Count == 0;

    public event EventHandler? Changed;

    public bool Contains(IEntity entity) => _items.Contains(entity);

    public void Set(IEntity entity)
    {
        _items.Clear();
        _items.Add(entity);
        OnChanged();
    }

    public void Add(IEntity entity)
    {
        if (_items.Contains(entity))
            return;

        _items.Add(entity);
        OnChanged();
    }

    public void Remove(IEntity entity)
    {
        if (_items.Remove(entity))
            OnChanged();
    }

    public void Toggle(IEntity entity)
    {
        if (!_items.Remove(entity))
            _items.Add(entity);
        OnChanged();
    }

    public void Clear()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
