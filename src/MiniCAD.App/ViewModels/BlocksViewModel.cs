using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The "Blöcke" palette: lists block definitions, creates one from the current selection
/// (replacing it with an instance), inserts the selected block via the insert tool, and removes
/// definitions.
/// </summary>
public partial class BlocksViewModel : ViewModelBase
{
    private readonly CadDocument _document;
    private readonly Selection _selection;
    private readonly UndoRedoManager _commands;
    private readonly ToolManager _tools;
    private readonly BlockInsertTool _insertTool;

    public BlocksViewModel(CadDocument document, Selection selection, UndoRedoManager commands,
        ToolManager tools, BlockInsertTool insertTool)
    {
        _document = document;
        _selection = selection;
        _commands = commands;
        _tools = tools;
        _insertTool = insertTool;

        _document.Changed += OnDocumentChanged;
        _selection.Changed += (_, _) => CreateBlockCommand.NotifyCanExecuteChanged();
        Rebuild();
    }

    public ObservableCollection<BlockDefinition> Blocks { get; } = new();

    [ObservableProperty]
    private BlockDefinition? _selectedBlock;

    [ObservableProperty]
    private string _newBlockName = string.Empty;

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.BlocksChanged or DocumentChangeKind.Reloaded)
            Rebuild();
    }

    private void Rebuild()
    {
        Guid? keep = SelectedBlock?.Id;
        Blocks.Clear();
        foreach (BlockDefinition definition in _document.BlockDefinitions)
            Blocks.Add(definition);
        SelectedBlock = Blocks.FirstOrDefault(b => keep is { } id && b.Id == id) ?? Blocks.FirstOrDefault();
    }

    private bool HasSelection => !_selection.IsEmpty;

    /// <summary>Makes a block from the selected entities and replaces them with one instance.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CreateBlock()
    {
        var items = _selection.Items.ToList();
        if (items.Count == 0)
            return;

        Rect2D bounds = items[0].Bounds;
        foreach (IEntity entity in items.Skip(1))
            bounds = bounds.Union(entity.Bounds);
        Point2D basePoint = bounds.Center;

        // Clone the geometry, baking each entity's resolved stroke so the block keeps its look.
        var clones = items.Select(e =>
        {
            IEntity clone = e.Clone();
            clone.StrokeOverride = _document.ResolveStroke(e);
            return clone;
        }).ToList();

        string name = string.IsNullOrWhiteSpace(NewBlockName) ? $"Block {_document.BlockDefinitions.Count + 1}" : NewBlockName.Trim();
        var definition = new BlockDefinition(name, basePoint, clones);
        _document.AddBlockDefinition(definition);

        // Replace the selection with a single instance, undoably.
        var reference = new BlockReferenceEntity(definition, basePoint);
        var commands = items.Select(e => (IUndoableCommand)new RemoveEntityCommand(_document, e)).ToList();
        commands.Add(new AddEntityCommand(_document, reference));
        _commands.Execute(new CompositeCommand("Block erstellen", commands));

        _selection.Set(reference);
        NewBlockName = string.Empty;
        SelectedBlock = Blocks.FirstOrDefault(b => b.Id == definition.Id);
    }

    /// <summary>Activates the insert tool for the selected block definition.</summary>
    [RelayCommand]
    private void InsertBlock()
    {
        if (SelectedBlock is { } definition)
        {
            _insertTool.Definition = definition;
            _tools.SetActiveTool(_insertTool);
        }
    }

    [RelayCommand]
    private void RemoveBlock()
    {
        if (SelectedBlock is { } definition)
            _document.RemoveBlockDefinition(definition);
    }
}
