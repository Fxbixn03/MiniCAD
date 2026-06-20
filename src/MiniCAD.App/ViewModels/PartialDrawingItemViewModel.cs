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
    }

    public PartialDrawing Model { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ElementState _state;

    /// <summary>State as a 0/1/2 index for the dropdown (Aktiviert / Gesperrt / Deaktiviert).</summary>
    public int StateIndex
    {
        get => (int)State;
        set => State = (ElementState)value;
    }

    partial void OnNameChanged(string value) => _document.RenamePartialDrawing(Model, value);

    partial void OnStateChanged(ElementState value)
    {
        OnPropertyChanged(nameof(StateIndex));
        _document.SetPartialDrawingState(Model, value);
    }
}
