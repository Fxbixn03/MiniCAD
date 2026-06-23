using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the double-line tool (width and justification), written through to the
/// <see cref="MultilineTool"/> (#236).
/// </summary>
public partial class MultilineOptionsViewModel : ViewModelBase
{
    private readonly MultilineTool _tool;

    public MultilineOptionsViewModel(MultilineTool tool)
    {
        _tool = tool;
        ApplyWidth(WidthText);
        _tool.Justification = Justification;
    }

    public MultilineJustification[] JustificationOptions { get; } = Enum.GetValues<MultilineJustification>();

    [ObservableProperty]
    private string _widthText = "240";

    [ObservableProperty]
    private MultilineJustification _justification = MultilineJustification.Center;

    partial void OnWidthTextChanged(string value) => ApplyWidth(value);

    partial void OnJustificationChanged(MultilineJustification value) => _tool.Justification = value;

    private void ApplyWidth(string text)
    {
        if (CoordinateFormat.TryParse(text, out double width) && width > 0)
            _tool.Width = width;
    }
}
