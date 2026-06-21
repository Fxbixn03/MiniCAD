using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class PartialDrawingTests
{
    [Fact]
    public void TeilbildHeights_AreSettableAndPersist()
    {
        var doc = new CadDocument();
        doc.SetPartialDrawingHeights(doc.DefaultPartialDrawing, baseHeight: 300, height: 275);

        MiniCAD.Core.Persistence.DocumentDto dto = MiniCAD.Core.Persistence.DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        MiniCAD.Core.Persistence.DocumentMapper.Apply(dto, restored);

        restored.DefaultPartialDrawing.BaseHeight.Should().Be(300);
        restored.DefaultPartialDrawing.Height.Should().Be(275);
    }

    [Fact]
    public void NewDocument_HasOneDefaultTeilbild()
    {
        var document = new CadDocument();

        document.PartialDrawings.Should().ContainSingle();
        document.ActivePartialDrawing.Should().Be(document.DefaultPartialDrawing);
        document.ActivePartialDrawing.Name.Should().Be("Teilbild 1");
    }

    [Fact]
    public void AddEntity_AssignsActiveTeilbild()
    {
        var document = new CadDocument();
        PartialDrawing second = document.AddPartialDrawing("Teilbild 2");
        document.ActivePartialDrawing = second;

        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        document.AddEntity(line);

        line.PartialDrawingId.Should().Be(second.Id);
    }

    [Fact]
    public void AddPartialDrawing_RaisesEvent()
    {
        var document = new CadDocument();
        DocumentChangeKind? kind = null;
        document.Changed += (_, e) => kind = e.Kind;

        document.AddPartialDrawing("Teilbild 2");

        kind.Should().Be(DocumentChangeKind.PartialDrawingAdded);
        document.PartialDrawings.Should().HaveCount(2);
    }

    [Fact]
    public void IsEntityVisible_RespectsTeilbildVisibility()
    {
        var document = new CadDocument();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        document.AddEntity(line);

        document.IsEntityVisible(line).Should().BeTrue();

        document.SetPartialDrawingState(document.ActivePartialDrawing, ElementState.Off);
        document.IsEntityVisible(line).Should().BeFalse();
    }

    [Fact]
    public void LockedTeilbild_IsVisibleButNotEditable()
    {
        var document = new CadDocument();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        document.AddEntity(line);

        document.SetPartialDrawingState(document.ActivePartialDrawing, ElementState.Locked);

        document.IsEntityVisible(line).Should().BeTrue();   // still drawn / snappable
        document.IsEntityEditable(line).Should().BeFalse(); // but read-only
    }

    [Fact]
    public void LockedLayer_IsVisibleButNotEditable()
    {
        var document = new CadDocument();
        Layer layer = document.AddLayer("Wände", StrokeStyle.Default);
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = layer.Id };
        document.AddEntity(line);

        document.SetLayerState(layer, ElementState.Locked);

        document.IsEntityVisible(line).Should().BeTrue();
        document.IsEntityEditable(line).Should().BeFalse();
    }

    [Fact]
    public void GetContentBounds_IgnoresHiddenTeilbilder()
    {
        var document = new CadDocument();
        PartialDrawing hidden = document.AddPartialDrawing("Versteckt");

        document.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(1, 1)));
        document.AddEntity(new LineEntity(new Point2D(100, 100), new Point2D(200, 200))
        {
            PartialDrawingId = hidden.Id,
        });

        document.SetPartialDrawingState(hidden, ElementState.Off);

        document.GetContentBounds().Should().Be(new Rect2D(0, 0, 1, 1));
    }

    [Fact]
    public void SetPartialDrawingState_RaisesModifiedEvent()
    {
        var document = new CadDocument();
        DocumentChangeKind? kind = null;
        document.Changed += (_, e) => kind = e.Kind;

        document.SetPartialDrawingState(document.ActivePartialDrawing, ElementState.Off);

        kind.Should().Be(DocumentChangeKind.PartialDrawingModified);
    }

    [Fact]
    public void MovePartialDrawing_ReordersStacking()
    {
        var document = new CadDocument();
        PartialDrawing first = document.DefaultPartialDrawing;
        PartialDrawing second = document.AddPartialDrawing("Teilbild 2");

        document.MovePartialDrawing(second, -1);

        document.PartialDrawings.Should().Equal(second, first);
    }

    [Fact]
    public void RenamePartialDrawing_UpdatesName()
    {
        var document = new CadDocument();

        document.RenamePartialDrawing(document.ActivePartialDrawing, "Grundriss");

        document.ActivePartialDrawing.Name.Should().Be("Grundriss");
    }
}
