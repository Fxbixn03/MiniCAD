using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Config.Net;

namespace MiniCAD.App.Configuration;

/// <summary>
/// App-wide settings singleton. Wraps the Config.Net proxy and turns the raw newline string
/// into a tidy recent-projects API. The backing JSON lives at
/// <c>{LocalApplicationData}/MiniCAD/config.json</c>, which resolves correctly on Windows,
/// Linux and macOS.
/// </summary>
public sealed class AppConfig
{
    private const int MaxRecent = 10;

    private readonly IAppConfig _config;

    public static AppConfig Instance { get; } = new();

    private AppConfig()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniCAD");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "config.json");

        _config = new ConfigurationBuilder<IAppConfig>()
            .UseJsonFile(path)
            .Build();
    }

    public bool OpenLastProjectOnStartup
    {
        get => _config.OpenLastProjectOnStartup;
        set => _config.OpenLastProjectOnStartup = value;
    }

    /// <summary>UI colour theme, "Light" or "Dark".</summary>
    public string Theme
    {
        get => _config.Theme;
        set => _config.Theme = value;
    }

    public bool ShowGrid
    {
        get => _config.ShowGrid;
        set => _config.ShowGrid = value;
    }

    /// <summary>Canvas background as a <c>#RRGGBB</c> hex string.</summary>
    public string BackgroundColorHex
    {
        get => _config.BackgroundColor;
        set => _config.BackgroundColor = value;
    }

    /// <summary>Custom keyboard shortcuts as a JSON map of action name → gesture string.</summary>
    public string Shortcuts
    {
        get => _config.Shortcuts;
        set => _config.Shortcuts = value;
    }

    /// <summary>Decimal separator used for coordinate input and the readout ("," or ".").</summary>
    public string DecimalSeparator
    {
        get => _config.DecimalSeparator;
        set => _config.DecimalSeparator = value;
    }

    /// <summary>User-defined Assistant templates serialized as JSON.</summary>
    public string AssistantTemplates
    {
        get => _config.AssistantTemplates;
        set => _config.AssistantTemplates = value;
    }

    /// <summary>Toolbar layout (dock edge, block order, hidden tools) serialized as JSON.</summary>
    public string ToolbarLayout
    {
        get => _config.ToolbarLayout;
        set => _config.ToolbarLayout = value;
    }

    /// <summary>Recent project paths, most-recent first.</summary>
    public IReadOnlyList<string> RecentProjects => Parse(_config.RecentProjects);

    /// <summary>The most recently used project path, or <c>null</c> if there is none.</summary>
    public string? LastProject => RecentProjects.FirstOrDefault();

    /// <summary>Moves <paramref name="path"/> to the top of the recent list (deduplicated, capped).</summary>
    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var list = Parse(_config.RecentProjects).ToList();
        list.RemoveAll(p => PathEquals(p, path));
        list.Insert(0, path);
        if (list.Count > MaxRecent)
            list.RemoveRange(MaxRecent, list.Count - MaxRecent);

        _config.RecentProjects = string.Join('\n', list);
    }

    public void RemoveRecentProject(string path)
    {
        var list = Parse(_config.RecentProjects).ToList();
        if (list.RemoveAll(p => PathEquals(p, path)) > 0)
            _config.RecentProjects = string.Join('\n', list);
    }

    private static bool PathEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Parse(string? raw)
        => string.IsNullOrEmpty(raw)
            ? Array.Empty<string>()
            : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
