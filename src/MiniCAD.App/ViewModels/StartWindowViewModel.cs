using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MiniCAD.App.Configuration;

namespace MiniCAD.App.ViewModels;

/// <summary>A recent project entry shown in the start window.</summary>
public sealed record RecentProjectItem(string Name, string Path);

/// <summary>
/// View model for the Visual-Studio-style start window: lists recent projects and offers
/// new/open actions. The actual window navigation is handled by the view's code-behind.
/// </summary>
public partial class StartWindowViewModel : ViewModelBase
{
    public StartWindowViewModel()
        : this(AppConfig.Instance.RecentProjects)
    {
    }

    public StartWindowViewModel(IEnumerable<string> recentPaths)
    {
        RecentProjects = new ObservableCollection<RecentProjectItem>(
            recentPaths
                .Where(File.Exists) // drop entries whose file was moved or deleted
                .Select(p => new RecentProjectItem(Path.GetFileNameWithoutExtension(p), p)));
    }

    public ObservableCollection<RecentProjectItem> RecentProjects { get; }

    public bool HasRecentProjects => RecentProjects.Count > 0;
}
