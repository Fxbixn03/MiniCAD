using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>Inline options for the linear dimension tool: the orientation kind.</summary>
public partial class DimensionOptionsViewModel : ViewModelBase
{
    private readonly LinearDimensionTool _tool;

    public DimensionOptionsViewModel(LinearDimensionTool tool)
    {
        _tool = tool;
        _tool.Kind = Kind;
        _tool.ContinueMode = ContinueMode;
    }

    public LinearDimensionKind[] KindOptions { get; } = Enum.GetValues<LinearDimensionKind>();

    public DimensionContinueMode[] ContinueModeOptions { get; } = Enum.GetValues<DimensionContinueMode>();

    [ObservableProperty]
    private LinearDimensionKind _kind = LinearDimensionKind.Aligned;

    [ObservableProperty]
    private DimensionContinueMode _continueMode = DimensionContinueMode.None;

    partial void OnKindChanged(LinearDimensionKind value) => _tool.Kind = value;

    partial void OnContinueModeChanged(DimensionContinueMode value) => _tool.ContinueMode = value;
}
