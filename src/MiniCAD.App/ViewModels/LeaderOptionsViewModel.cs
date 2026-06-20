using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>Inline options for the leader tool (label cap height), written through to the tool.</summary>
public partial class LeaderOptionsViewModel : ViewModelBase
{
    private readonly LeaderTool _tool;

    public LeaderOptionsViewModel(LeaderTool tool)
    {
        _tool = tool;
        ApplyHeight(HeightText);
    }

    [ObservableProperty]
    private string _heightText = "12";

    partial void OnHeightTextChanged(string value) => ApplyHeight(value);

    private void ApplyHeight(string text)
    {
        if (CoordinateFormat.TryParse(text, out double height) && height > 0)
            _tool.TextHeight = height;
    }
}
