using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Styling;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// Drives the side panel that manages Teilbilder (partial drawings) and layers: create,
/// rename, toggle visibility, reorder Teilbilder, and pick the active one (= list selection)
/// into which new geometry is drawn.
/// </summary>
public partial class StructurePanelViewModel : ViewModelBase
{
    private readonly CadDocument _document;

    public StructurePanelViewModel(CadDocument document)
    {
        _document = document;
        _document.Changed += OnDocumentChanged;
        Rebuild();
    }

    public ObservableCollection<PartialDrawingItemViewModel> PartialDrawings { get; } = new();

    public ObservableCollection<LayerItemViewModel> Layers { get; } = new();

    /// <summary>The selected Teilbild is the active one for new geometry.</summary>
    [ObservableProperty]
    private PartialDrawingItemViewModel? _selectedPartialDrawing;

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;

    partial void OnSelectedPartialDrawingChanged(PartialDrawingItemViewModel? value)
    {
        if (value is not null)
        {
            _document.ActivePartialDrawing = value.Model;
            RefreshPartialDrawingStatuses(); // the "Aktuell" badge moves to the new target
        }
    }

    private void RefreshPartialDrawingStatuses()
    {
        foreach (PartialDrawingItemViewModel item in PartialDrawings)
            item.RefreshStatus();
    }

    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        if (value is not null)
            _document.ActiveLayer = value.Model;
    }

    [RelayCommand]
    private void AddPartialDrawing()
    {
        PartialDrawing partialDrawing = _document.AddPartialDrawing($"Teilbild {_document.PartialDrawings.Count}");
        _document.ActivePartialDrawing = partialDrawing;
        RebuildPartialDrawings();
    }

    [RelayCommand]
    private void AddLayer()
    {
        Layer layer = _document.AddLayer($"Layer {_document.Layers.Count}", new StrokeStyle(Color.White, 1.0));
        _document.ActiveLayer = layer;
        RebuildLayers();
    }

    /// <summary>Adds the professional standard layer set (skipping any already present).</summary>
    [RelayCommand]
    private void AddStandardLayers()
    {
        if (_document.AddStandardLayers() > 0)
            RebuildLayers();
    }

    [RelayCommand]
    private void MovePartialDrawingUp() => MoveSelectedPartialDrawing(-1);

    [RelayCommand]
    private void MovePartialDrawingDown() => MoveSelectedPartialDrawing(1);

    private void MoveSelectedPartialDrawing(int delta)
    {
        if (SelectedPartialDrawing is not { } item)
            return;

        int index = PartialDrawings.IndexOf(item);
        int target = index + delta;
        if (target < 0 || target >= PartialDrawings.Count)
            return;

        _document.MovePartialDrawing(item.Model, delta);
        PartialDrawings.Move(index, target);
        SelectedPartialDrawing = item;
    }

    /// <summary>
    /// Re-syncs the list selections with the document's active layer/Teilbild. Call before
    /// showing a manager so quick-select adoptions on the canvas are reflected.
    /// </summary>
    public void SyncSelectionFromDocument()
    {
        SelectedPartialDrawing = PartialDrawings.FirstOrDefault(i => i.Model == _document.ActivePartialDrawing);
        SelectedLayer = Layers.FirstOrDefault(i => i.Model == _document.ActiveLayer);
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        // A loaded project replaces the structure wholesale.
        if (e.Kind == DocumentChangeKind.Reloaded)
            Rebuild();
        else if (e.Kind == DocumentChangeKind.PartialDrawingModified)
            RefreshPartialDrawingStatuses();
    }

    private void Rebuild()
    {
        RebuildPartialDrawings();
        RebuildLayers();
    }

    private void RebuildPartialDrawings()
    {
        PartialDrawings.Clear();
        foreach (PartialDrawing partialDrawing in _document.PartialDrawings)
            PartialDrawings.Add(new PartialDrawingItemViewModel(_document, partialDrawing));

        SelectedPartialDrawing = PartialDrawings.FirstOrDefault(i => i.Model == _document.ActivePartialDrawing);
    }

    private void RebuildLayers()
    {
        Layers.Clear();
        foreach (Layer layer in _document.Layers)
            Layers.Add(new LayerItemViewModel(_document, layer));

        SelectedLayer = Layers.FirstOrDefault(i => i.Model == _document.ActiveLayer);
    }
}
