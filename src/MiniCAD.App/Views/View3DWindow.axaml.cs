using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniCAD.App.ViewModels;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Viewing;

namespace MiniCAD.App.Views;

/// <summary>
/// A floating 3D view window. It shares the document and the render mode with the main window (so
/// the model and its shading stay in sync), but navigates with its own camera.
/// </summary>
public partial class View3DWindow : Window
{
    private CadDocument? _document;
    private Camera3D? _camera;

    public View3DWindow()
    {
        InitializeComponent();
    }

    public View3DWindow(MainWindowViewModel viewModel, CadDocument document, Camera3D camera) : this()
    {
        // Bindings (Mode/ShowGround) resolve against the main view model; geometry uses this camera.
        DataContext = viewModel;
        _document = document;
        _camera = camera;
        View3D.Document = document;
        View3D.Camera = camera;
    }

    private void SetView(StandardView view)
    {
        if (_camera is null)
            return;
        _camera.SetStandardView(view);
        if (_document?.GetModelBounds() is { } bounds)
            _camera.ZoomToFit(bounds);
    }

    private void OnIso(object? sender, RoutedEventArgs e) => SetView(StandardView.Iso);
    private void OnTop(object? sender, RoutedEventArgs e) => SetView(StandardView.Top);
    private void OnFront(object? sender, RoutedEventArgs e) => SetView(StandardView.Front);
    private void OnRight(object? sender, RoutedEventArgs e) => SetView(StandardView.Right);
}
