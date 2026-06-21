using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the wall (and beam) tool: the reference line (Anschlag — centerline, left or
/// right face) plus thickness and height. Values are written straight to the tools so the next
/// drawn wall uses them.
/// </summary>
public partial class WallOptionsViewModel : ViewModelBase
{
    private readonly WallTool _wall;
    private readonly BeamTool _beam;

    public WallOptionsViewModel(WallTool wall, BeamTool beam)
    {
        _wall = wall;
        _beam = beam;
    }

    [ObservableProperty]
    private string _thicknessText = "240";

    [ObservableProperty]
    private string _heightText = "2500";

    [ObservableProperty]
    private DrawReference _reference;

    public bool IsCenter => Reference == DrawReference.Center;
    public bool IsLeft => Reference == DrawReference.Left;
    public bool IsRight => Reference == DrawReference.Right;

    /// <summary>Sets the Anschlag from the inline buttons ("Center" / "Left" / "Right").</summary>
    [RelayCommand]
    private void SetReference(string reference) => Reference = reference switch
    {
        "Left" => DrawReference.Left,
        "Right" => DrawReference.Right,
        _ => DrawReference.Center,
    };

    partial void OnReferenceChanged(DrawReference value)
    {
        _wall.Reference = value;
        _beam.Reference = value;
        OnPropertyChanged(nameof(IsCenter));
        OnPropertyChanged(nameof(IsLeft));
        OnPropertyChanged(nameof(IsRight));
    }

    partial void OnThicknessTextChanged(string value)
    {
        if (CoordinateFormat.TryParse(value, out double t) && t > 0)
            _wall.Thickness = t;
    }

    partial void OnHeightTextChanged(string value)
    {
        if (CoordinateFormat.TryParse(value, out double h) && h > 0)
            _wall.Height = h;
    }
}
