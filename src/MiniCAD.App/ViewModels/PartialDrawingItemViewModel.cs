using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Documents;

namespace MiniCAD.App.ViewModels;

/// <summary>A single Teilbild row in the structure panel (editable name + visibility).</summary>
public partial class PartialDrawingItemViewModel : ViewModelBase
{
    private readonly CadDocument _document;

    public PartialDrawingItemViewModel(CadDocument document, PartialDrawing model)
    {
        _document = document;
        Model = model;
        _name = model.Name;
        _state = model.State;
        _scaleDenominator = (int)model.ReferenceScale;
    }

    public PartialDrawing Model { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ElementState _state;

    /// <summary>Reference scale denominator (the "n" in 1:n) for this Teilbild.</summary>
    [ObservableProperty]
    private int _scaleDenominator;

    partial void OnScaleDenominatorChanged(int value)
    {
        if (value > 0)
            _document.SetPartialDrawingReferenceScale(Model, value);
    }

    /// <summary>State as a 0/1/2 index for the dropdown (Aktiv / Passiv / Aus).</summary>
    public int StateIndex
    {
        get => (int)State;
        set => State = (ElementState)value;
    }

    /// <summary>The Allplan four-state status (Aktuell / Aktiv / Passiv / Aus).</summary>
    public PartialDrawingStatus Status => _document.GetPartialDrawingStatus(Model);

    public string StatusLabel => Status switch
    {
        PartialDrawingStatus.Current => "Aktuell",
        PartialDrawingStatus.Active => "Aktiv",
        PartialDrawingStatus.Passive => "Passiv",
        _ => "Aus",
    };

    /// <summary>True for the current (drawing-target) Teilbild, which drives the "Aktuell" badge.</summary>
    public bool IsCurrent => Status == PartialDrawingStatus.Current;

    /// <summary>Recomputes the derived status (after the current Teilbild or a state changes).</summary>
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsCurrent));
    }

    partial void OnNameChanged(string value) => _document.RenamePartialDrawing(Model, value);

    partial void OnStateChanged(ElementState value)
    {
        OnPropertyChanged(nameof(StateIndex));
        _document.SetPartialDrawingState(Model, value);
        RefreshStatus();
    }
}
