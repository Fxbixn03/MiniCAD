using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Configuration;
using MiniCAD.App.Input;
using MiniCAD.App.ViewModels.Toolbar;

namespace MiniCAD.App.ViewModels;

/// <summary>One entry in the settings navigation list (left side), searchable by name + keywords.</summary>
public sealed class SettingsSectionViewModel
{
    public SettingsSectionViewModel(string key, string name, string keywords)
    {
        Key = key;
        Name = name;
        Keywords = keywords;
    }

    public string Key { get; }

    public string Name { get; }

    /// <summary>Searchable text (representative content of the section).</summary>
    public string Keywords { get; }

    public bool Matches(string query)
        => Name.Contains(query, System.StringComparison.OrdinalIgnoreCase)
           || Keywords.Contains(query, System.StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Live view settings shared between the main window's canvas and the settings dialog.
/// Both bind to the same instance, so changes in the dialog update the canvas immediately.
/// All values are loaded from and persisted to <see cref="AppConfig"/> (Config.Net).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Color DefaultBackground = Color.FromRgb(10, 20, 48);

    [ObservableProperty]
    private bool _showGrid;

    /// <summary>When true, the app uses the dark colour theme instead of the light one.</summary>
    [ObservableProperty]
    private bool _isDarkMode;

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
        _isDarkMode = IsDarkTheme(config.Theme);
        _openLastProjectOnStartup = config.OpenLastProjectOnStartup;

        Color background = ParseColor(config.BackgroundColorHex);
        _backgroundRed = background.R;
        _backgroundGreen = background.G;
        _backgroundBlue = background.B;
        _useDecimalPoint = config.DecimalSeparator == ".";

        Shortcuts = new ShortcutsViewModel(shortcuts);

        _allSections = new List<SettingsSectionViewModel>
        {
            new("general", "Allgemein", "start projekt zuletzt laden eingabe dezimal komma punkt trennzeichen"),
            new("display", "Darstellung", "design dark dunkel hell theme raster grid hintergrund farbe canvas zeichenfläche"),
            new("toolbar", "Werkzeugleiste", "werkzeug tool leiste toolbar anzeigen ausblenden ein aus andocken position blöcke"),
            new("shortcuts", "Tastenkürzel", "taste tasten kürzel gesten shortcut belegung"),
        };
        Sections = new ObservableCollection<SettingsSectionViewModel>(_allSections);
        _selectedSection = _allSections[0];
    }

    /// <summary>The customizable keyboard-shortcut page ("Tastenkürzel").</summary>
    public ShortcutsViewModel Shortcuts { get; }

    /// <summary>The configurable toolbar (assigned by the main view model after construction).</summary>
    public ToolbarViewModel? Toolbar { get; set; }

    // ----- Sectioned navigation + search -----

    private readonly List<SettingsSectionViewModel> _allSections;

    /// <summary>The (search-filtered) navigation entries shown on the left.</summary>
    public ObservableCollection<SettingsSectionViewModel> Sections { get; }

    [ObservableProperty]
    private SettingsSectionViewModel? _selectedSection;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>The large title shown top-right (the current section's name).</summary>
    public string SelectedSectionTitle => SelectedSection?.Name ?? string.Empty;

    public bool IsGeneral => SelectedSection?.Key == "general";
    public bool IsDisplay => SelectedSection?.Key == "display";
    public bool IsToolbar => SelectedSection?.Key == "toolbar";
    public bool IsShortcuts => SelectedSection?.Key == "shortcuts";

    partial void OnSelectedSectionChanged(SettingsSectionViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(IsDisplay));
        OnPropertyChanged(nameof(IsToolbar));
        OnPropertyChanged(nameof(IsShortcuts));
    }

    partial void OnSearchTextChanged(string value)
    {
        string query = value.Trim();
        IEnumerable<SettingsSectionViewModel> matches = string.IsNullOrEmpty(query)
            ? _allSections
            : _allSections.Where(s => s.Matches(query));

        Sections.Clear();
        foreach (SettingsSectionViewModel section in matches)
            Sections.Add(section);

        // Keep a valid selection: prefer the current one, else the first match.
        if (SelectedSection is null || !Sections.Contains(SelectedSection))
            SelectedSection = Sections.FirstOrDefault();
    }

    /// <summary>The composed background color the canvas clears to.</summary>
    public Color BackgroundColor => Color.FromRgb((byte)BackgroundRed, (byte)BackgroundGreen, (byte)BackgroundBlue);

    /// <summary>Brush form of <see cref="BackgroundColor"/> for the dialog's preview swatch.</summary>
    public IBrush BackgroundBrush => new SolidColorBrush(BackgroundColor);

    public string BackgroundHex => $"#{(byte)BackgroundRed:X2}{(byte)BackgroundGreen:X2}{(byte)BackgroundBlue:X2}";

    partial void OnShowGridChanged(bool value) => AppConfig.Instance.ShowGrid = value;

    partial void OnIsDarkModeChanged(bool value)
    {
        AppConfig.Instance.Theme = value ? "Dark" : "Light";
        ApplyTheme(value);
    }

    /// <summary>Pushes the chosen variant onto the running application so chrome swaps live.</summary>
    public static void ApplyTheme(bool dark)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static bool IsDarkTheme(string? theme)
        => string.Equals(theme, "Dark", System.StringComparison.OrdinalIgnoreCase);

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
