using Avalonia.Controls;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Viewing;

namespace MiniCAD.App.Views;

/// <summary>
/// A floating 3D view window. It shares the document with the main window (so the model stays
/// live in both) but navigates with its own camera.
/// </summary>
public partial class View3DWindow : Window
{
    public View3DWindow()
    {
        InitializeComponent();
    }

    public View3DWindow(CadDocument document, Camera3D camera) : this()
    {
        View3D.Document = document;
        View3D.Camera = camera;
    }
}
