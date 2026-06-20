using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The inline mode selector for the arc tool (shown while it is active): 3-point versus
/// centre/start/end. The choice is written straight through to the <see cref="ArcTool"/>.
/// </summary>
public partial class ArcOptionsViewModel : ViewModelBase
{
    private readonly ArcTool _tool;

    public ArcOptionsViewModel(ArcTool tool)
    {
        _tool = tool;
        _tool.Mode = ArcMode.ThreePoint;
    }

    /// <summary>True = centre/start/end, false = 3-point.</summary>
    [ObservableProperty]
    private bool _isCenterMode;

    public string ModeLabel => IsCenterMode ? "Mittelpunkt" : "3-Punkt";

    public string Hint => IsCenterMode
        ? "Mittelpunkt, Startpunkt, dann Endrichtung klicken."
        : "Start, Punkt auf dem Bogen, dann Endpunkt klicken.";

    partial void OnIsCenterModeChanged(bool value)
    {
        _tool.Mode = value ? ArcMode.CenterStartEnd : ArcMode.ThreePoint;
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(Hint));
    }
}
