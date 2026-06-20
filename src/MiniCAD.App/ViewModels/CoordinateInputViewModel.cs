using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The Allplan-style coordinate entry line. While a drawing tool is active the user can type an
/// X/Y coordinate and place the point with Enter instead of clicking. Values are absolute (from
/// the Nullpunkt) or relative (from the last point); the decimal separator follows the setting.
/// </summary>
public partial class CoordinateInputViewModel : ViewModelBase
{
    private readonly ToolManager _tools;
    private readonly CadDocument _document;

    public CoordinateInputViewModel(ToolManager tools, CadDocument document)
    {
        _tools = tools;
        _document = document;
    }

    [ObservableProperty]
    private string _xText = "0";

    [ObservableProperty]
    private string _yText = "0";

    /// <summary>Absolute (from the origin) vs. relative (from the last placed point).</summary>
    [ObservableProperty]
    private bool _isRelative;

    /// <summary>True only while a point-placing tool is active.</summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>Transient validation hint shown next to the fields.</summary>
    [ObservableProperty]
    private string _hint = string.Empty;

    public string ModeLabel => IsRelative ? "Relativ" : "Absolut";

    partial void OnIsRelativeChanged(bool value) => OnPropertyChanged(nameof(ModeLabel));

    /// <summary>Parses the X/Y fields and places the point through the active tool.</summary>
    [RelayCommand]
    private void Commit()
    {
        if (!IsEnabled)
            return;

        if (!CoordinateFormat.TryParse(XText, out double x) || !CoordinateFormat.TryParse(YText, out double y))
        {
            Hint = "Ungültige Koordinate";
            return;
        }

        Point2D origin = _document.CoordinateSystem.Origin.ToPoint2D();
        Point2D basePoint = IsRelative ? (_tools.LastPoint ?? origin) : origin;
        var world = new Point2D(basePoint.X + x, basePoint.Y + y);

        _tools.CommitCoordinate(world);
        Hint = string.Empty;
    }
}
