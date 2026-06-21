using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Tests;

public class ModelCommandTests
{
    [Fact]
    public void TransformModelCommand_AppliesAndUndoes()
    {
        var doc = new CadDocument();
        var model = new Model3DObject(Mesh3D.Box(100, 100, 100));
        doc.AddModelObject(model);

        Matrix4 before = model.Transform;
        Matrix4 after = before * Matrix4.Translation(new Vector3D(500, 0, 0));
        var cmd = new TransformModelCommand(doc, model, before, after);

        cmd.Execute();
        model.Bounds.Center.X.Should().BeApproximately(500, 1e-6);

        cmd.Undo();
        model.Bounds.Center.X.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void AddAndRemoveModelCommands_AreUndoable()
    {
        var doc = new CadDocument();
        var model = new Model3DObject(Mesh3D.Box(1, 1, 1));

        var add = new AddModelCommand(doc, model);
        add.Execute();
        doc.Models.Should().Contain(model);
        add.Undo();
        doc.Models.Should().NotContain(model);

        doc.AddModelObject(model);
        var remove = new RemoveModelCommand(doc, model);
        remove.Execute();
        doc.Models.Should().NotContain(model);
        remove.Undo();
        doc.Models.Should().Contain(model);
    }
}
