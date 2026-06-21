using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>Inline options for the radial dimension tool: radius vs. diameter.</summary>
public partial class RadialDimensionOptionsViewModel : ViewModelBase
{
    private readonly RadialDimensionTool _tool;

    public RadialDimensionOptionsViewModel(RadialDimensionTool tool)
    {
        _tool = tool;
        _tool.Diameter = IsDiameter;
    }

    [ObservableProperty]
    private bool _isDiameter;

    public string ModeLabel => IsDiameter ? "Durchmesser ⌀" : "Radius R";

    partial void OnIsDiameterChanged(bool value)
    {
        _tool.Diameter = value;
        OnPropertyChanged(nameof(ModeLabel));
    }
}
