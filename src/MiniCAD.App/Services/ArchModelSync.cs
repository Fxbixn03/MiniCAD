using System.Linq;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Model3D;

namespace MiniCAD.App.Services;

/// <summary>
/// Keeps the live 3D model in sync with the 2D architectural entities (#73). Whenever the document
/// changes, the derived model objects (walls today) are rebuilt from their 2D sources, so drawing
/// or editing a wall in the plan is immediately reflected in the 3D view — including the detached
/// 3D window, which shares the same document. Derived models are flagged so they stay out of
/// persistence and never collide with the user's manually inserted/extruded solids.
/// </summary>
public sealed class ArchModelSync
{
    private readonly CadDocument _document;
    private bool _busy;

    public ArchModelSync(CadDocument document)
    {
        _document = document;
        _document.Changed += OnChanged;
        Rebuild();
    }

    private void OnChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (_busy)
            return; // ignore the ModelsChanged events our own rebuild raises

        if (e.Kind is DocumentChangeKind.EntityAdded or DocumentChangeKind.EntityRemoved
            or DocumentChangeKind.EntityModified or DocumentChangeKind.Cleared
            or DocumentChangeKind.Reloaded)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _busy = true;
        try
        {
            foreach (Model3DObject derived in _document.Models.Where(m => m.IsDerived).ToList())
                _document.RemoveModelObject(derived);

            foreach (WallEntity wall in _document.Entities.OfType<WallEntity>())
                _document.AddModelObject(WallModelBuilder.BuildModel(wall));
        }
        finally
        {
            _busy = false;
        }
    }
}
