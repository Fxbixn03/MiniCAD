using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The inline parameter line for the fillet/chamfer tool (shown at the bottom while it is
/// active). Values are written straight through to the <see cref="FilletTool"/> so the next
/// pick uses them.
/// </summary>
public partial class FilletOptionsViewModel : ViewModelBase
{
    private readonly FilletTool _tool;

    public FilletOptionsViewModel(FilletTool tool)
    {
        _tool = tool;
        _tool.Distance = 0;
        _tool.Chamfer = false;
    }

    /// <summary>Fillet radius, or chamfer setback, as typed text (parsed leniently).</summary>
    [ObservableProperty]
    private string _distanceText = "0";

    /// <summary>Bevel (chamfer) instead of round (fillet).</summary>
    [ObservableProperty]
    private bool _isChamfer;

    public string DistanceLabel => IsChamfer ? "ABSTAND" : "RADIUS";

    public string ModeLabel => IsChamfer ? "Fase" : "Abrundung";

    partial void OnDistanceTextChanged(string value)
    {
        if (CoordinateFormat.TryParse(value, out double distance) && distance >= 0)
            _tool.Distance = distance;
    }

    partial void OnIsChamferChanged(bool value)
    {
        _tool.Chamfer = value;
        OnPropertyChanged(nameof(DistanceLabel));
        OnPropertyChanged(nameof(ModeLabel));
    }
}
