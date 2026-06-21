using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Documents;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Backs the Allplan-style Teilbild selection dialog: a checkbox list of all Teilbilder whose
/// status can be set in bulk, plus making one the current (drawing-target) Teilbild.
/// </summary>
public partial class PartialDrawingSelectionViewModel : ViewModelBase
{
    private readonly CadDocument _document;

    public PartialDrawingSelectionViewModel(CadDocument document)
    {
        _document = document;
        Rows = new ObservableCollection<PartialDrawingItemViewModel>(
            document.PartialDrawings.Select(pd => new PartialDrawingItemViewModel(document, pd)));
    }

    public ObservableCollection<PartialDrawingItemViewModel> Rows { get; }

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    private void SetAll(bool selected)
    {
        foreach (PartialDrawingItemViewModel row in Rows)
            row.IsSelected = selected;
    }

    [RelayCommand]
    private void SetActive() => ApplyState(ElementState.Active);

    [RelayCommand]
    private void SetPassive() => ApplyState(ElementState.Locked);

    [RelayCommand]
    private void SetOff() => ApplyState(ElementState.Off);

    private void ApplyState(ElementState state)
    {
        foreach (PartialDrawingItemViewModel row in Rows.Where(r => r.IsSelected))
            row.State = state; // row applies it through the document and refreshes its status
    }

    /// <summary>Makes the first checked Teilbild the current (drawing-target) one.</summary>
    [RelayCommand]
    private void MakeCurrent()
    {
        if (Rows.FirstOrDefault(r => r.IsSelected) is { } row)
        {
            _document.SetCurrentPartialDrawing(row.Model);
            foreach (PartialDrawingItemViewModel item in Rows)
                item.RefreshStatus();
        }
    }
}
