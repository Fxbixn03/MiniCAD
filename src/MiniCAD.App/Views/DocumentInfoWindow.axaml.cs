using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;

namespace MiniCAD.App.Views;

/// <summary>
/// A read-only document statistics report: object counts by type/layer/Teilbild, the number of
/// definitions of each kind and the drawing's 2D/3D extents. It refreshes live while open (#235).
/// </summary>
public partial class DocumentInfoWindow : Window
{
    private readonly CadDocument? _document;

    public DocumentInfoWindow()
    {
        InitializeComponent();
    }

    public DocumentInfoWindow(CadDocument document) : this()
    {
        _document = document;
        _document.Changed += OnDocumentChanged;
        Closed += (_, _) => _document.Changed -= OnDocumentChanged;
        Refresh();
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_document is null)
            return;

        DocumentStatistics stats = DocumentStatistics.Compute(_document);

        ObjectsText.Text = stats.ConstructionCount > 0
            ? $"Objekte: {stats.EntityCount} (davon {stats.ConstructionCount} Hilfskonstruktion)"
            : $"Objekte: {stats.EntityCount}";
        DefinitionsText.Text =
            $"Layer: {stats.LayerCount}  ·  Teilbilder: {stats.PartialDrawingCount}  ·  Blöcke: {stats.BlockDefinitionCount}\n" +
            $"Textstile: {stats.TextStyleCount}  ·  Maßstile: {stats.DimStyleCount}  ·  Muster: {stats.PatternCount}";
        BoundsText.Text = "Zeichnungsgrenzen: " + FormatBounds(stats.ContentBounds);
        ModelText.Text = stats.ModelCount > 0
            ? $"3D-Modelle: {stats.ModelCount}"
            : "3D-Modelle: keine";

        TypesHost.ItemsSource = stats.CountsByType;
        LayersHost.ItemsSource = stats.CountsByLayer;
        PartialDrawingsHost.ItemsSource = stats.CountsByPartialDrawing;
    }

    private static string FormatBounds(Rect2D? bounds)
    {
        if (bounds is not { } b)
            return "leer";

        return string.Create(CultureInfo.CurrentCulture,
            $"X {b.MinX:0.##} … {b.MaxX:0.##}, Y {b.MinY:0.##} … {b.MaxY:0.##}  (B {b.Width:0.##} × H {b.Height:0.##})");
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
