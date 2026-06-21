using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Configuration;

namespace MiniCAD.App.ViewModels.Toolbar;

/// <summary>Where the tool rail is docked.</summary>
public enum ToolbarDock
{
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>A reorderable block of related tools in the toolbar.</summary>
public partial class ToolGroupViewModel : ViewModelBase
{
    public ToolGroupViewModel(string id, string name, IEnumerable<ToolbarItemViewModel> items)
    {
        Id = id;
        Name = name;
        Items = new ObservableCollection<ToolbarItemViewModel>(items);
    }

    public string Id { get; }

    public string Name { get; }

    public ObservableCollection<ToolbarItemViewModel> Items { get; }

    /// <summary>Layout orientation of this block's items (kept in sync with the toolbar's dock).</summary>
    [ObservableProperty]
    private Orientation _orientation = Orientation.Vertical;
}

/// <summary>
/// The configurable tool rail: an ordered set of tool blocks plus a dock edge. The user can move
/// blocks and dock the whole bar (left/right/top/bottom); individual tools can be hidden. The
/// layout (dock, block order, hidden tools) is persisted to <see cref="AppConfig"/>.
/// </summary>
public partial class ToolbarViewModel : ViewModelBase
{
    private bool _loading;

    public ToolbarViewModel(IEnumerable<ToolGroupViewModel> groups)
    {
        Groups = new ObservableCollection<ToolGroupViewModel>(groups);
        AllItems = Groups.SelectMany(g => g.Items).ToList();

        foreach (ToolbarItemViewModel item in AllItems)
            item.PropertyChanged += (_, e) =>
            {
                if (!_loading && e.PropertyName == nameof(ToolbarItemViewModel.IsVisible))
                    SaveLayout();
            };

        LoadLayout();
        ApplyOrientation();
    }

    private void ApplyOrientation()
    {
        foreach (ToolGroupViewModel group in Groups)
            group.Orientation = Orientation;
    }

    public ObservableCollection<ToolGroupViewModel> Groups { get; }

    /// <summary>All tools across all groups (for the Settings show/hide list).</summary>
    public IReadOnlyList<ToolbarItemViewModel> AllItems { get; }

    [ObservableProperty]
    private ToolbarDock _dock = ToolbarDock.Left;

    /// <summary>Layout orientation of the tool blocks (vertical when docked left/right).</summary>
    public Orientation Orientation => Dock is ToolbarDock.Left or ToolbarDock.Right
        ? Orientation.Vertical
        : Orientation.Horizontal;

    /// <summary>Where to dock the toolbar border within the editor's outer DockPanel.</summary>
    public Avalonia.Controls.Dock PanelDock => Dock switch
    {
        ToolbarDock.Right => Avalonia.Controls.Dock.Right,
        ToolbarDock.Top => Avalonia.Controls.Dock.Top,
        ToolbarDock.Bottom => Avalonia.Controls.Dock.Bottom,
        _ => Avalonia.Controls.Dock.Left,
    };

    /// <summary>Where the dock-chooser handle sits within the bar (top for vertical, left for horizontal).</summary>
    public Avalonia.Controls.Dock HandleDock => Dock is ToolbarDock.Left or ToolbarDock.Right
        ? Avalonia.Controls.Dock.Top
        : Avalonia.Controls.Dock.Left;

    partial void OnDockChanged(ToolbarDock value)
    {
        OnPropertyChanged(nameof(Orientation));
        OnPropertyChanged(nameof(PanelDock));
        OnPropertyChanged(nameof(HandleDock));
        ApplyOrientation();
        if (!_loading)
            SaveLayout();
    }

    /// <summary>Re-reads every tool's active/checked state (after a tool change or toggle).</summary>
    public void RefreshAll()
    {
        foreach (ToolbarItemViewModel item in AllItems)
            item.Refresh();
    }

    /// <summary>Shifts a group by <paramref name="delta"/> positions (context-menu reorder fallback).</summary>
    public void MoveGroupBy(string id, int delta)
    {
        ToolGroupViewModel? g = Groups.FirstOrDefault(x => x.Id == id);
        if (g is null)
            return;
        int i = Groups.IndexOf(g);
        int j = System.Math.Clamp(i + delta, 0, Groups.Count - 1);
        if (i == j)
            return;
        Groups.Move(i, j);
        if (!_loading)
            SaveLayout();
    }

    /// <summary>Moves the group with <paramref name="sourceId"/> to be before/after <paramref name="targetId"/>.</summary>
    public void MoveGroup(string sourceId, string targetId)
    {
        if (sourceId == targetId)
            return;
        ToolGroupViewModel? source = Groups.FirstOrDefault(g => g.Id == sourceId);
        if (source is null)
            return;
        int from = Groups.IndexOf(source);
        int to = Groups.IndexOf(Groups.FirstOrDefault(g => g.Id == targetId)!);
        if (to < 0)
            return;
        Groups.Move(from, to);
        if (!_loading)
            SaveLayout();
    }

    // ----- Persistence -----

    private sealed class LayoutDto
    {
        public string Dock { get; set; } = "Left";
        public List<string> Order { get; set; } = new();
        public List<string> Hidden { get; set; } = new();
    }

    private void SaveLayout()
    {
        var dto = new LayoutDto
        {
            Dock = Dock.ToString(),
            Order = Groups.Select(g => g.Id).ToList(),
            Hidden = AllItems.Where(i => !i.IsVisible).Select(i => i.Id).ToList(),
        };
        AppConfig.Instance.ToolbarLayout = JsonSerializer.Serialize(dto);
    }

    private void LoadLayout()
    {
        string raw = AppConfig.Instance.ToolbarLayout;
        if (string.IsNullOrWhiteSpace(raw))
            return;

        LayoutDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<LayoutDto>(raw);
        }
        catch
        {
            return;
        }
        if (dto is null)
            return;

        _loading = true;
        try
        {
            if (System.Enum.TryParse(dto.Dock, out ToolbarDock dock))
                Dock = dock;

            // Reorder groups to match the saved order (unknown/new groups keep their relative order at the end).
            for (int target = 0; target < dto.Order.Count; target++)
            {
                ToolGroupViewModel? g = Groups.FirstOrDefault(x => x.Id == dto.Order[target]);
                if (g is null)
                    continue;
                int current = Groups.IndexOf(g);
                int dest = System.Math.Min(target, Groups.Count - 1);
                if (current != dest)
                    Groups.Move(current, dest);
            }

            var hidden = dto.Hidden.ToHashSet();
            foreach (ToolbarItemViewModel item in AllItems)
                item.IsVisible = !hidden.Contains(item.Id);
        }
        finally
        {
            _loading = false;
        }
    }
}
