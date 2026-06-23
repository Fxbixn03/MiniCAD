using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the donut tool (inner radius), written through to the
/// <see cref="DonutTool"/> (#196).
/// </summary>
public partial class DonutOptionsViewModel : ViewModelBase
{
    private readonly DonutTool _tool;

    public DonutOptionsViewModel(DonutTool tool)
    {
        _tool = tool;
        ApplyInner(InnerText);
    }

    [ObservableProperty]
    private string _innerText = "5";

    partial void OnInnerTextChanged(string value) => ApplyInner(value);

    private void ApplyInner(string text)
    {
        if (CoordinateFormat.TryParse(text, out double radius) && radius > 0)
            _tool.InnerRadius = radius;
    }
}
