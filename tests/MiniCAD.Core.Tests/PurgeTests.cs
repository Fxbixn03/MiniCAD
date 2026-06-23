using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class PurgeTests
{
    [Fact]
    public void Purge_RemovesUnusedLayer_KeepsUsedAndProtected()
    {
        var document = new CadDocument();
        Layer used = document.AddLayer("Used", new StrokeStyle(Color.Red, 1));
        Layer unused = document.AddLayer("Unused", new StrokeStyle(Color.Blue, 1));
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = used.Id });

        PurgeReport report = document.PurgeUnused();

        report.Layers.Should().ContainSingle().Which.Should().BeSameAs(unused);
        document.Layers.Should().Contain(used).And.NotContain(unused);
        document.Layers.Should().Contain(document.DefaultLayer);
    }

    [Fact]
    public void Purge_NeverRemovesActiveLayer_EvenIfEmpty()
    {
        var document = new CadDocument();
        Layer active = document.AddLayer("Active", new StrokeStyle(Color.Red, 1));
        document.ActiveLayer = active;

        PurgeReport report = document.PurgeUnused();

        report.Layers.Should().NotContain(active);
        document.Layers.Should().Contain(active);
    }

    [Fact]
    public void Purge_RemovesUnusedTextStyle_KeepsAssignedOne()
    {
        var document = new CadDocument();
        TextStyle assigned = document.AddTextStyle("Bold");
        document.AddTextStyle("Orphan");
        var text = new TextEntity(Point2D.Origin, "Hi") { TextStyleId = assigned.Id };
        document.AddEntity(text);

        PurgeReport report = document.PurgeUnused();

        report.TextStyles.Select(s => s.Name).Should().ContainSingle().Which.Should().Be("Orphan");
        document.TextStyles.Should().Contain(assigned);
        document.TextStyles.Should().Contain(document.DefaultTextStyle);
    }

    [Fact]
    public void Purge_RemovesUnreferencedBlockDefinition_KeepsReferenced()
    {
        var document = new CadDocument();
        var usedDef = new BlockDefinition("Used", Point2D.Origin, new[] { new LineEntity(Point2D.Origin, new Point2D(1, 0)) });
        var orphanDef = new BlockDefinition("Orphan", Point2D.Origin, new[] { new LineEntity(Point2D.Origin, new Point2D(0, 1)) });
        document.AddBlockDefinition(usedDef);
        document.AddBlockDefinition(orphanDef);
        document.AddEntity(new BlockReferenceEntity(usedDef, new Point2D(5, 5)));

        PurgeReport report = document.PurgeUnused();

        report.BlockDefinitions.Should().ContainSingle().Which.Should().BeSameAs(orphanDef);
        document.BlockDefinitions.Should().Contain(usedDef).And.NotContain(orphanDef);
    }

    [Fact]
    public void Purge_RemovesBlockUsedOnlyByAnotherUnusedBlock()
    {
        var document = new CadDocument();
        var inner = new BlockDefinition("Inner", Point2D.Origin, new[] { new LineEntity(Point2D.Origin, new Point2D(1, 0)) });
        var outer = new BlockDefinition("Outer", Point2D.Origin, new IEntity[] { new BlockReferenceEntity(inner, Point2D.Origin) });
        document.AddBlockDefinition(inner);
        document.AddBlockDefinition(outer);
        // Neither block is placed in the drawing → both unused, transitively.

        PurgeReport report = document.PurgeUnused();

        report.BlockDefinitions.Should().BeEquivalentTo(new[] { inner, outer });
        document.BlockDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Purge_RemovesEmptyPartialDrawing_KeepsOccupiedAndProtected()
    {
        var document = new CadDocument();
        PartialDrawing occupied = document.AddPartialDrawing("Occupied");
        PartialDrawing empty = document.AddPartialDrawing("Empty");
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)) { PartialDrawingId = occupied.Id });

        PurgeReport report = document.PurgeUnused();

        report.PartialDrawings.Should().ContainSingle().Which.Should().BeSameAs(empty);
        document.PartialDrawings.Should().Contain(occupied).And.NotContain(empty);
        document.PartialDrawings.Should().Contain(document.DefaultPartialDrawing);
    }

    [Fact]
    public void PurgeOptions_DisablingACategory_KeepsThoseDefinitions()
    {
        var document = new CadDocument();
        document.AddLayer("Unused", new StrokeStyle(Color.Blue, 1));

        PurgeReport report = document.PurgeUnused(new PurgeOptions { Layers = false });

        report.Layers.Should().BeEmpty();
        document.Layers.Should().HaveCount(2);
    }

    [Fact]
    public void PurgeCommand_Undo_RestoresRemovedDefinitions()
    {
        var document = new CadDocument();
        Layer unused = document.AddLayer("Unused", new StrokeStyle(Color.Blue, 1));
        var manager = new UndoRedoManager();

        PurgeReport report = document.FindPurgeable();
        report.Layers.Should().Contain(unused);
        manager.Execute(new PurgeCommand(document, report));
        document.Layers.Should().NotContain(unused);

        manager.Undo();
        document.Layers.Should().Contain(unused);
        document.FindLayer(unused.Id).Should().BeSameAs(unused);

        manager.Redo();
        document.Layers.Should().NotContain(unused);
    }

    [Fact]
    public void FindPurgeable_DoesNotMutate()
    {
        var document = new CadDocument();
        document.AddLayer("Unused", new StrokeStyle(Color.Blue, 1));

        PurgeReport report = document.FindPurgeable();

        report.Layers.Should().HaveCount(1);
        document.Layers.Should().HaveCount(2, "FindPurgeable only previews");
    }
}
