using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>A symbol file in the cross-project library.</summary>
public sealed class LibrarySymbol
{
    public LibrarySymbol(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public string Path { get; }
    public string Name { get; }

    public override string ToString() => Name;
}

/// <summary>
/// The "Bibliothek" palette: saves a document block into the cross-project library, lists the
/// stored symbols with a thumbnail preview, and inserts a chosen symbol into the document.
/// </summary>
public partial class LibraryViewModel : ViewModelBase
{
    private const int PreviewWidth = 220;
    private const int PreviewHeight = 150;

    private readonly CadDocument _document;
    private readonly ToolManager _tools;
    private readonly BlockInsertTool _insertTool;

    public LibraryViewModel(CadDocument document, ToolManager tools, BlockInsertTool insertTool)
    {
        _document = document;
        _tools = tools;
        _insertTool = insertTool;
        _document.Changed += OnDocumentChanged;
        RebuildDocumentBlocks();
        RefreshLibrary();
    }

    public ObservableCollection<BlockDefinition> DocumentBlocks { get; } = new();

    [ObservableProperty]
    private BlockDefinition? _selectedDocumentBlock;

    public ObservableCollection<LibrarySymbol> Symbols { get; } = new();

    [ObservableProperty]
    private LibrarySymbol? _selectedSymbol;

    [ObservableProperty]
    private Bitmap? _preview;

    [ObservableProperty]
    private string _status = string.Empty;

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.BlocksChanged or DocumentChangeKind.Reloaded)
            RebuildDocumentBlocks();
    }

    private void RebuildDocumentBlocks()
    {
        DocumentBlocks.Clear();
        foreach (BlockDefinition definition in _document.BlockDefinitions)
            DocumentBlocks.Add(definition);
        SelectedDocumentBlock ??= DocumentBlocks.FirstOrDefault();
    }

    [RelayCommand]
    private void RefreshLibrary()
    {
        Symbols.Clear();
        foreach (string path in SymbolLibrary.ListFiles())
            Symbols.Add(new LibrarySymbol(path));
        SelectedSymbol = Symbols.FirstOrDefault();
    }

    [RelayCommand]
    private void SaveToLibrary()
    {
        if (SelectedDocumentBlock is not { } definition)
            return;

        try
        {
            SymbolLibrary.Save(definition);
            Status = $"„{definition.Name}“ in Bibliothek gespeichert.";
            RefreshLibrary();
        }
        catch (Exception ex)
        {
            Status = $"Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private void InsertSymbol()
    {
        if (SelectedSymbol is not { } symbol)
            return;

        try
        {
            BlockDefinition definition = SymbolLibrary.Load(symbol.Path);
            _document.AddBlockDefinition(definition);
            _insertTool.Definition = definition;
            _tools.SetActiveTool(_insertTool);
            Status = $"„{definition.Name}“ – Einfügepunkt klicken.";
        }
        catch (Exception ex)
        {
            Status = $"Fehler: {ex.Message}";
        }
    }

    partial void OnSelectedSymbolChanged(LibrarySymbol? value)
    {
        Preview = null;
        if (value is null)
            return;

        try
        {
            BlockDefinition definition = SymbolLibrary.Load(value.Path);
            Preview = SymbolPreviewRenderer.Render(definition, PreviewWidth, PreviewHeight);
        }
        catch (IOException)
        {
            // Ignore unreadable/locked files in the preview.
        }
    }
}
