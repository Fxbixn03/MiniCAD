using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The inline parameter line for the array tool (shown at the bottom while it is active).
/// Rectangular fields (rows/columns/spacing) or polar fields (count/angle) are toggled by
/// <see cref="IsPolar"/>; every value is written through to the <see cref="ArrayTool"/>.
/// </summary>
public partial class ArrayOptionsViewModel : ViewModelBase
{
    private readonly ArrayTool _tool;

    public ArrayOptionsViewModel(ArrayTool tool)
    {
        _tool = tool;
        _tool.Polar = IsPolar;
        Apply(RowsText, n => _tool.Rows = n);
        Apply(ColumnsText, n => _tool.Columns = n);
        Apply(CountText, n => _tool.Count = n);
        ApplyDouble(SpacingXText, v => _tool.SpacingX = v);
        ApplyDouble(SpacingYText, v => _tool.SpacingY = v);
        ApplyDouble(AngleText, v => _tool.TotalAngle = GeometryMath.DegreesToRadians(v));
    }

    [ObservableProperty]
    private bool _isPolar;

    [ObservableProperty]
    private string _rowsText = "2";

    [ObservableProperty]
    private string _columnsText = "3";

    [ObservableProperty]
    private string _spacingXText = "10";

    [ObservableProperty]
    private string _spacingYText = "10";

    [ObservableProperty]
    private string _countText = "6";

    [ObservableProperty]
    private string _angleText = "360";

    public string ModeLabel => IsPolar ? "Polar" : "Rechteckig";

    partial void OnIsPolarChanged(bool value)
    {
        _tool.Polar = value;
        OnPropertyChanged(nameof(ModeLabel));
    }

    partial void OnRowsTextChanged(string value) => Apply(value, n => _tool.Rows = n);

    partial void OnColumnsTextChanged(string value) => Apply(value, n => _tool.Columns = n);

    partial void OnCountTextChanged(string value) => Apply(value, n => _tool.Count = n);

    partial void OnSpacingXTextChanged(string value) => ApplyDouble(value, v => _tool.SpacingX = v);

    partial void OnSpacingYTextChanged(string value) => ApplyDouble(value, v => _tool.SpacingY = v);

    partial void OnAngleTextChanged(string value)
        => ApplyDouble(value, v => _tool.TotalAngle = GeometryMath.DegreesToRadians(v));

    private static void Apply(string text, Action<int> set)
    {
        if (int.TryParse(text, out int value) && value > 0)
            set(value);
    }

    private static void ApplyDouble(string text, Action<double> set)
    {
        if (CoordinateFormat.TryParse(text, out double value))
            set(value);
    }
}
