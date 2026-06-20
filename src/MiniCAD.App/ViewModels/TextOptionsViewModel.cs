using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the text tool: the active text style (font/width factor), per-text cap
/// height, alignment and single/multi-line. Style/height feed the tool and document; the style
/// list mirrors the document's text styles.
/// </summary>
public partial class TextOptionsViewModel : ViewModelBase
{
    private readonly TextTool _tool;
    private readonly CadDocument _document;
    private bool _suppress;

    public TextOptionsViewModel(TextTool tool, CadDocument document)
    {
        _tool = tool;
        _document = document;
        _tool.HorizontalAlignment = HorizontalAlignment;
        _tool.VerticalAlignment = VerticalAlignment;
        _tool.Multiline = Multiline;
        ApplyHeight(HeightText);

        _document.Changed += OnDocumentChanged;
        RebuildStyles();
    }

    public ObservableCollection<TextStyle> Styles { get; } = new();

    public TextHAlign[] HorizontalOptions { get; } = Enum.GetValues<TextHAlign>();

    public TextVAlign[] VerticalOptions { get; } = Enum.GetValues<TextVAlign>();

    [ObservableProperty]
    private TextStyle? _activeStyle;

    [ObservableProperty]
    private string _heightText = "12";

    [ObservableProperty]
    private TextHAlign _horizontalAlignment = TextHAlign.Left;

    [ObservableProperty]
    private TextVAlign _verticalAlignment = TextVAlign.Baseline;

    [ObservableProperty]
    private bool _multiline;

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.TextStylesChanged or DocumentChangeKind.Reloaded)
            RebuildStyles();
    }

    private void RebuildStyles()
    {
        _suppress = true;
        Styles.Clear();
        foreach (TextStyle style in _document.TextStyles)
            Styles.Add(style);
        ActiveStyle = _document.ActiveTextStyle;
        _suppress = false;
    }

    partial void OnActiveStyleChanged(TextStyle? value)
    {
        if (_suppress || value is null)
            return;
        _document.ActiveTextStyle = value;
    }

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
