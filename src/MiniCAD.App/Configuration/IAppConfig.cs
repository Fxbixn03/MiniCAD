using Config.Net;

namespace MiniCAD.App.Configuration;

/// <summary>
/// Config.Net-backed application settings (persisted as JSON in the user's local app-data
/// folder, cross-platform). Recent project paths are stored as a single newline-separated
/// string to stay robust across Config.Net versions.
/// </summary>
public interface IAppConfig
{
    /// <summary>When true, the app opens the last project directly instead of the start window.</summary>
    [Option(DefaultValue = false)]
    bool OpenLastProjectOnStartup { get; set; }

    /// <summary>Newline-separated absolute project paths, most-recently-used first.</summary>
    [Option(DefaultValue = "")]
    string RecentProjects { get; set; }

    /// <summary>UI colour theme: "Light" (default) or "Dark".</summary>
    [Option(DefaultValue = "Light")]
    string Theme { get; set; }

    /// <summary>Whether the canvas reference grid is shown.</summary>
    [Option(DefaultValue = true)]
    bool ShowGrid { get; set; }

    /// <summary>Canvas background color as a <c>#RRGGBB</c> hex string (the navy blueprint surface).</summary>
    [Option(DefaultValue = "#0A1430")]
    string BackgroundColor { get; set; }

    /// <summary>Decimal separator for coordinate input/readout — "," (default) or ".".</summary>
    [Option(DefaultValue = ",")]
    string DecimalSeparator { get; set; }

    /// <summary>Custom keyboard shortcuts as a JSON map of action name → gesture string.</summary>
    [Option(DefaultValue = "")]
    string Shortcuts { get; set; }

    /// <summary>User-defined Assistant templates ("Vorlagen") as a JSON array.</summary>
    [Option(DefaultValue = "")]
    string AssistantTemplates { get; set; }
}
