using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the text tool (cap height, alignment, single/multi-line), written
/// straight through to the <see cref="TextTool"/>.
/// </summary>
public partial class TextOptionsViewModel : ViewModelBase
{
    private readonly TextTool _tool;

    public TextOptionsViewModel(TextTool tool)
    {
        _tool = tool;
        _tool.HorizontalAlignment = HorizontalAlignment;
        _tool.VerticalAlignment = VerticalAlignment;
        _tool.Multiline = Multiline;
        ApplyHeight(HeightText);
    }

    public TextHAlign[] HorizontalOptions { get; } = Enum.GetValues<TextHAlign>();

    public TextVAlign[] VerticalOptions { get; } = Enum.GetValues<TextVAlign>();

    [ObservableProperty]
    private string _heightText = "12";

    [ObservableProperty]
    private TextHAlign _horizontalAlignment = TextHAlign.Left;

    [ObservableProperty]
    private TextVAlign _verticalAlignment = TextVAlign.Baseline;

    [ObservableProperty]
    private bool _multiline;

    partial void OnHeightTextChanged(string value) => ApplyHeight(value);

    partial void OnHorizontalAlignmentChanged(TextHAlign value) => _tool.HorizontalAlignment = value;

    partial void OnVerticalAlignmentChanged(TextVAlign value) => _tool.VerticalAlignment = value;

    partial void OnMultilineChanged(bool value) => _tool.Multiline = value;

    private void ApplyHeight(string text)
    {
        if (CoordinateFormat.TryParse(text, out double height) && height > 0)
            _tool.Height = height;
    }
}
