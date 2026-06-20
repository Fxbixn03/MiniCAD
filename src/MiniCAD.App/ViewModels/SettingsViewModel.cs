using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Configuration;
using MiniCAD.App.Input;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Live view settings shared between the main window's canvas and the settings dialog.
/// Both bind to the same instance, so changes in the dialog update the canvas immediately.
/// All values are loaded from and persisted to <see cref="AppConfig"/> (Config.Net).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Color DefaultBackground = Color.FromRgb(30, 30, 34);

    [ObservableProperty]
    private bool _showGrid;

    /// <summary>When enabled, the app opens the last project on startup.</summary>
    [ObservableProperty]
    private bool _openLastProjectOnStartup;

    [ObservableProperty]
    private double _backgroundRed;

    [ObservableProperty]
    private double _backgroundGreen;

    [ObservableProperty]
    private double _backgroundBlue;

    /// <summary>When true, coordinates use a point as the decimal separator instead of a comma.</summary>
    [ObservableProperty]
    private bool _useDecimalPoint;

    /// <summary>Designer/previewer constructor — owns its own shortcut model.</summary>
    public SettingsViewModel() : this(new KeyboardShortcuts())
    {
    }

    public SettingsViewModel(KeyboardShortcuts shortcuts)
    {
        AppConfig config = AppConfig.Instance;
        _showGrid = config.ShowGrid;
        _openLastProjectOnStartup = config.OpenLastProjectOnStartup;

        Color background = ParseColor(config.BackgroundColorHex);
        _backgroundRed = background.R;
        _backgroundGreen = background.G;
        _backgroundBlue = background.B;
        _useDecimalPoint = config.DecimalSeparator == ".";

        Shortcuts = new ShortcutsViewModel(shortcuts);
    }

    /// <summary>The customizable keyboard-shortcut page ("Tastenkürzel").</summary>
    public ShortcutsViewModel Shortcuts { get; }

    /// <summary>The composed background color the canvas clears to.</summary>
    public Color BackgroundColor => Color.FromRgb((byte)BackgroundRed, (byte)BackgroundGreen, (byte)BackgroundBlue);

    /// <summary>Brush form of <see cref="BackgroundColor"/> for the dialog's preview swatch.</summary>
    public IBrush BackgroundBrush => new SolidColorBrush(BackgroundColor);

    public string BackgroundHex => $"#{(byte)BackgroundRed:X2}{(byte)BackgroundGreen:X2}{(byte)BackgroundBlue:X2}";

    partial void OnShowGridChanged(bool value) => AppConfig.Instance.ShowGrid = value;

    partial void OnUseDecimalPointChanged(bool value)
        => AppConfig.Instance.DecimalSeparator = value ? "." : ",";

    partial void OnOpenLastProjectOnStartupChanged(bool value)
        => AppConfig.Instance.OpenLastProjectOnStartup = value;

    partial void OnBackgroundRedChanged(double value) => RaiseBackgroundChanged();

    partial void OnBackgroundGreenChanged(double value) => RaiseBackgroundChanged();

    partial void OnBackgroundBlueChanged(double value) => RaiseBackgroundChanged();

    private void RaiseBackgroundChanged()
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(BackgroundHex));
        AppConfig.Instance.BackgroundColorHex = BackgroundHex;
    }

    private static Color ParseColor(string hex)
        => Color.TryParse(hex, out Color color) ? color : DefaultBackground;
}
