using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>Inline options for the elevation (Höhenkote) tool: the height value to place.</summary>
public partial class ElevationDimensionOptionsViewModel : ViewModelBase
{
    private readonly ElevationDimensionTool _tool;

    public ElevationDimensionOptionsViewModel(ElevationDimensionTool tool)
    {
        _tool = tool;
        ApplyZ(ZText);
    }

    [ObservableProperty]
    private string _zText = "0";

    partial void OnZTextChanged(string value) => ApplyZ(value);

    private void ApplyZ(string text)
    {
        if (CoordinateFormat.TryParse(text, out double z))
            _tool.ZValue = z;
    }
}
