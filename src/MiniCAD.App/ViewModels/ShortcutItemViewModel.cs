using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;

namespace MiniCAD.App.ViewModels;

/// <summary>One editable row in the shortcuts settings page: a bindable action plus its key.</summary>
public partial class ShortcutItemViewModel : ViewModelBase
{
    private readonly KeyboardShortcuts _shortcuts;
    private bool _suppress;

    public ShortcutItemViewModel(KeyboardShortcuts shortcuts, ShortcutAction action, string label)
    {
        _shortcuts = shortcuts;
        Action = action;
        Label = label;
        _gesture = shortcuts.Get(action);
    }

    public ShortcutAction Action { get; }

    public string Label { get; }

    /// <summary>The currently assigned gesture string (e.g. "Ctrl+S"); empty means unbound.</summary>
    [ObservableProperty]
    private string _gesture;

    partial void OnGestureChanged(string value)
    {
        if (_suppress)
            return;

        _shortcuts.Set(Action, value);
    }

    [RelayCommand]
    private void Reset() => Gesture = KeyboardShortcuts.DefaultFor(Action);

    /// <summary>Refreshes the displayed gesture from the model without re-persisting.</summary>
    public void Refresh()
    {
        _suppress = true;
        Gesture = _shortcuts.Get(Action);
        _suppress = false;
    }
}
