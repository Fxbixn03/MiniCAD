using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniCAD.App.ViewModels.Toolbar;

/// <summary>
/// One tool in the configurable toolbar — either a command button (optionally showing an "active"
/// state) or a toggle bound to a boolean state. Icon, label and tooltip are fixed; visibility is
/// user-configurable (Settings ▸ Werkzeuge) and persisted.
/// </summary>
public partial class ToolbarItemViewModel : ViewModelBase
{
    private readonly Func<bool>? _isActiveFunc;
    private readonly Func<bool>? _getChecked;
    private readonly Action<bool>? _setChecked;
    private bool _suppress;

    /// <summary>A command button (e.g. "Linie"); <paramref name="isActive"/> drives the highlight.</summary>
    public ToolbarItemViewModel(string id, string label, string tooltip, string icon,
        ICommand command, Func<bool>? isActive = null)
    {
        Id = id;
        Label = label;
        Tooltip = tooltip;
        Icon = ResolveIcon(icon);
        Command = command;
        _isActiveFunc = isActive;
    }

    /// <summary>A toggle button bound to a boolean state (e.g. Snap on/off).</summary>
    public ToolbarItemViewModel(string id, string label, string tooltip, string icon,
        Func<bool> getChecked, Action<bool> setChecked)
    {
        Id = id;
        Label = label;
        Tooltip = tooltip;
        Icon = ResolveIcon(icon);
        IsToggle = true;
        _getChecked = getChecked;
        _setChecked = setChecked;
        _isChecked = getChecked();
    }

    public string Id { get; }

    public string Label { get; }

    public string Tooltip { get; }

    public Geometry? Icon { get; }

    public bool IsToggle { get; }

    public ICommand? Command { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>Whether the tool is shown in the toolbar (user-configurable, persisted).</summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>Re-reads the active/checked state from the underlying source.</summary>
    public void Refresh()
    {
        _suppress = true;
        if (_isActiveFunc is not null)
            IsActive = _isActiveFunc();
        if (_getChecked is not null)
            IsChecked = _getChecked();
        _suppress = false;
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (_suppress)
            return;
        _setChecked?.Invoke(value);
    }

    private static Geometry? ResolveIcon(string keyOrData)
    {
        if (string.IsNullOrEmpty(keyOrData))
            return null;

        // Known icons are resource keys ("Icon.X"); the rest are raw path geometry data.
        if (keyOrData.StartsWith("Icon.", StringComparison.Ordinal)
            && Application.Current is { } app
            && app.TryGetResource(keyOrData, app.ActualThemeVariant, out object? res)
            && res is Geometry geometry)
        {
            return geometry;
        }

        try
        {
            return StreamGeometry.Parse(keyOrData);
        }
        catch
        {
            return null;
        }
    }
}
