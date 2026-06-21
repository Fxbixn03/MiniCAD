using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Commands;

/// <summary>Changes a 3D model object's world transform (move/rotate/scale), undoably.</summary>
public sealed class TransformModelCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly Model3DObject _model;
    private readonly Matrix4 _before;
    private readonly Matrix4 _after;

    public TransformModelCommand(ICadDocument document, Model3DObject model, Matrix4 before, Matrix4 after)
    {
        _document = document;
        _model = model;
        _before = before;
        _after = after;
    }

    public string Name => "3D-Transformation";

    public void Execute() { _model.Transform = _after; _document.NotifyModelModified(); }

    public void Undo() { _model.Transform = _before; _document.NotifyModelModified(); }
}

/// <summary>Adds a 3D model object, undoably (used for duplicate).</summary>
public sealed class AddModelCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly Model3DObject _model;

    public AddModelCommand(ICadDocument document, Model3DObject model)
    {
        _document = document;
        _model = model;
    }

    public string Name => "3D-Körper hinzufügen";

    public void Execute() => _document.AddModelObject(_model);

    public void Undo() => _document.RemoveModelObject(_model);
}

/// <summary>Removes a 3D model object, undoably.</summary>
public sealed class RemoveModelCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly Model3DObject _model;

    public RemoveModelCommand(ICadDocument document, Model3DObject model)
    {
        _document = document;
        _model = model;
    }

    public string Name => "3D-Körper löschen";

    public void Execute() => _document.RemoveModelObject(_model);

    public void Undo() => _document.AddModelObject(_model);
}
