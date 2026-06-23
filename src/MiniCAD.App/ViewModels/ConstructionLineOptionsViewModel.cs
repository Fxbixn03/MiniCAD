using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the construction-line tool: line (both ways) vs ray (half-infinite),
/// written through to the <see cref="ConstructionLineTool"/> (#195).
/// </summary>
public partial class ConstructionLineOptionsViewModel : ViewModelBase
{
    private readonly ConstructionLineTool _tool;

    public ConstructionLineOptionsViewModel(ConstructionLineTool tool)
    {
        _tool = tool;
        _tool.Ray = Ray;
    }

    [ObservableProperty]
    private bool _ray;

    public string ModeLabel => Ray ? "Strahl" : "Hilfslinie";

    partial void OnRayChanged(bool value)
    {
        _tool.Ray = value;
        OnPropertyChanged(nameof(ModeLabel));
    }
}
