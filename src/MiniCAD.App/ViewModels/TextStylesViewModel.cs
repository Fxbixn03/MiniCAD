using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;
using MiniCAD.Core.Documents;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The "Textstile" management panel: lists the document's text styles and edits the selected
/// one's name, font, default height and width factor. Edits go through the document so they
/// re-apply to every assigned text. New styles can be added and non-default ones removed.
/// </summary>
public partial class TextStylesViewModel : ViewModelBase
{
    private readonly CadDocument _document;
    private bool _suppress;

    public TextStylesViewModel(CadDocument document)
    {
        _document = document;
        _document.Changed += OnDocumentChanged;
        Rebuild();
    }

    public ObservableCollection<TextStyle> Styles { get; } = new();

    [ObservableProperty]
    private TextStyle? _selectedStyle;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fontFamily = string.Empty;

    [ObservableProperty]
    private string _heightText = "12";

    [ObservableProperty]
    private string _widthFactorText = "1";

    [ObservableProperty]
    private bool _canEdit;

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.TextStylesChanged or DocumentChangeKind.Reloaded)
            Rebuild();
    }

    private void Rebuild()
    {
        _suppress = true;
        Guid? keepId = SelectedStyle?.Id;

        Styles.Clear();
        foreach (TextStyle style in _document.TextStyles)
            Styles.Add(style);

        SelectedStyle = Styles.FirstOrDefault(s => s.Id == keepId) ?? _document.ActiveTextStyle;
        _suppress = false;
        LoadFields();
    }

    partial void OnSelectedStyleChanged(TextStyle? value) => LoadFields();

    private void LoadFields()
    {
        _suppress = true;
        CanEdit = SelectedStyle is not null;
        if (SelectedStyle is { } style)
        {
            Name = style.Name;
            FontFamily = style.FontFamily;
            HeightText = CoordinateFormat.ToText(style.Height, "0.##");
            WidthFactorText = CoordinateFormat.ToText(style.WidthFactor, "0.##");
        }

        _suppress = false;
    }

    partial void OnNameChanged(string value)
    {
        if (_suppress || SelectedStyle is not { } style || string.IsNullOrWhiteSpace(value))
            return;
        _document.RenameTextStyle(style, value);
    }

    partial void OnFontFamilyChanged(string value) => ApplyAppearance();

    partial void OnHeightTextChanged(string value) => ApplyAppearance();

    partial void OnWidthFactorTextChanged(string value) => ApplyAppearance();

    private void ApplyAppearance()
    {
        if (_suppress || SelectedStyle is not { } style)
            return;

        double height = CoordinateFormat.TryParse(HeightText, out double h) && h > 0 ? h : style.Height;
        double widthFactor = CoordinateFormat.TryParse(WidthFactorText, out double w) && w > 0 ? w : style.WidthFactor;
        _document.UpdateTextStyle(style, FontFamily ?? string.Empty, height, widthFactor);
    }

    [RelayCommand]
    private void AddStyle()
    {
        TextStyle style = _document.AddTextStyle($"Stil {_document.TextStyles.Count + 1}");
        SelectedStyle = Styles.FirstOrDefault(s => s.Id == style.Id);
    }

    [RelayCommand]
    private void RemoveStyle()
    {
        if (SelectedStyle is { } style)
            _document.RemoveTextStyle(style);
    }
}
