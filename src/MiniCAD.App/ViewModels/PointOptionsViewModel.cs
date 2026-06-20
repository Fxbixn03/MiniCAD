using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The inline marker options for the point tool (glyph style and size), written through to the
/// <see cref="PointTool"/>.
/// </summary>
public partial class PointOptionsViewModel : ViewModelBase
{
    private readonly PointTool _tool;

    public PointOptionsViewModel(PointTool tool)
    {
        _tool = tool;
        _tool.Style = Style;
        ApplySize(SizeText);
    }

    public PointStyle[] StyleOptions { get; } = Enum.GetValues<PointStyle>();

    [ObservableProperty]
    private PointStyle _style = PointStyle.Plus;

    [ObservableProperty]
    private string _sizeText = "6";

    partial void OnStyleChanged(PointStyle value) => _tool.Style = value;

    partial void OnSizeTextChanged(string value) => ApplySize(value);

    private void ApplySize(string text)
    {
        if (CoordinateFormat.TryParse(text, out double size) && size > 0)
            _tool.Size = size;
    }
}
