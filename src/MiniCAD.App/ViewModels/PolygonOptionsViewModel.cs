using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Inline options for the regular-polygon tool (side count and inscribed/circumscribed), written
/// through to the <see cref="RegularPolygonTool"/> (#196).
/// </summary>
public partial class PolygonOptionsViewModel : ViewModelBase
{
    private readonly RegularPolygonTool _tool;

    public PolygonOptionsViewModel(RegularPolygonTool tool)
    {
        _tool = tool;
        ApplySides(SidesText);
        _tool.Circumscribed = Circumscribed;
    }

    [ObservableProperty]
    private string _sidesText = "6";

    [ObservableProperty]
    private bool _circumscribed;

    public string ModeLabel => Circumscribed ? "Umschrieben" : "Einbeschrieben";

    partial void OnSidesTextChanged(string value) => ApplySides(value);

    partial void OnCircumscribedChanged(bool value)
    {
        _tool.Circumscribed = value;
        OnPropertyChanged(nameof(ModeLabel));
    }

    private void ApplySides(string text)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int sides)
            && sides >= RegularPolygonFactory.MinSides)
        {
            _tool.Sides = sides;
        }
    }
}
