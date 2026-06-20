using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Backs the "Tastenkürzel" settings page: lists every bindable action with its current key
/// and lets the user reassign or reset them. Edits flow straight into the shared
/// <see cref="KeyboardShortcuts"/> model (and on to <see cref="Configuration.AppConfig"/>),
/// so the main window picks them up immediately.
/// </summary>
public partial class ShortcutsViewModel : ViewModelBase
{
    private readonly KeyboardShortcuts _shortcuts;

    public ShortcutsViewModel(KeyboardShortcuts shortcuts)
    {
        _shortcuts = shortcuts;
        foreach ((ShortcutAction action, string label, _) in KeyboardShortcuts.Definitions)
            Items.Add(new ShortcutItemViewModel(shortcuts, action, label));
    }

    public ObservableCollection<ShortcutItemViewModel> Items { get; } = new();

    [RelayCommand]
    private void ResetAll()
    {
        foreach (ShortcutItemViewModel item in Items)
            item.Gesture = KeyboardShortcuts.DefaultFor(item.Action);
    }
}
