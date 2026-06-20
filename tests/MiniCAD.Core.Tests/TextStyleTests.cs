using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class TextStyleTests
{
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;
        public ICadDocument Document { get; }
        public Selection Selection { get; } = new();
        public double PickTolerance { get; set; } = 0.5;
        public StrokeStyle? DefaultStrokeOverride { get; set; }
        public UndoRedoManager Commands { get; } = new();
        public void Execute(IUndoableCommand command) => Commands.Execute(command);
        public void Push(IUndoableCommand command) => Commands.Push(command);
        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);
        public void RequestRedraw() { }
        public void ReturnToDefaultTool() { }
    }

    [Fact]
    public void Document_HasADefaultTextStyle()
    {
        var doc = new CadDocument();

        doc.TextStyles.Should().ContainSingle();
        doc.ActiveTextStyle.Should().Be(doc.DefaultTextStyle);
        doc.DefaultTextStyle.Name.Should().Be("Standard");
    }

    [Fact]
    public void AddAndRemoveTextStyle_Works_ButDefaultIsProtected()
    {
        var doc = new CadDocument();
        TextStyle style = doc.AddTextStyle("Überschrift", "Arial", 24, 1.0);

        doc.TextStyles.Should().HaveCount(2);
        doc.FindTextStyle(style.Id).Should().Be(style);

        doc.RemoveTextStyle(doc.DefaultTextStyle).Should().BeFalse(); // default protected
        doc.RemoveTextStyle(style).Should().BeTrue();
        doc.TextStyles.Should().ContainSingle();
    }

    [Fact]
    public void UpdateTextStyle_PropagatesToAssignedTexts()
    {
        var doc = new CadDocument();
        TextStyle style = doc.AddTextStyle("Titel", "Arial", 20, 1.0);

        var text = new TextEntity(Point2D.Origin, "Hallo");
        CadDocument.ApplyTextStyle(text, style);
        doc.AddEntity(text);
        text.Height.Should().Be(20);

        doc.UpdateTextStyle(style, "Consolas", 30, 1.2);

        text.FontFamily.Should().Be("Consolas");
        text.Height.Should().Be(30);
        text.WidthFactor.Should().Be(1.2);
    }

    [Fact]
    public void RemoveTextStyle_ReassignsAffectedTextsToDefault()
    {
        var doc = new CadDocument();
        TextStyle style = doc.AddTextStyle("Weg", "Arial", 18, 1.0);
        var text = new TextEntity(Point2D.Origin, "x");
        CadDocument.ApplyTextStyle(text, style);
        doc.AddEntity(text);

        doc.RemoveTextStyle(style);

        text.TextStyleId.Should().Be(doc.DefaultTextStyle.Id);
    }

    [Fact]
    public void TextTool_NewText_AdoptsActiveStyleFontAndWidthFactor()
    {
        var doc = new CadDocument();
        TextStyle style = doc.AddTextStyle("Schmal", "Consolas", 12, 0.7);
        doc.ActiveTextStyle = style;

        var ctx = new TestContext(doc);
        var tool = new TextTool { Height = 15 };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(1, 1), Button = ToolButton.Left });
        tool.Commit("Beschriftung");

        var text = doc.Entities.OfType<TextEntity>().Should().ContainSingle().Subject;
        text.TextStyleId.Should().Be(style.Id);
        text.FontFamily.Should().Be("Consolas");
        text.WidthFactor.Should().Be(0.7);
        text.Height.Should().Be(15); // per-text height from the tool options bar
    }

    [Fact]
    public void TextStyles_SurvivePersistenceRoundTrip()
    {
        var doc = new CadDocument();
        TextStyle style = doc.AddTextStyle("Titel", "Arial", 22, 1.1);
        doc.ActiveTextStyle = style;
        var text = new TextEntity(new Point2D(2, 2), "Hi");
        CadDocument.ApplyTextStyle(text, style);
        doc.AddEntity(text);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        TextStyle restoredStyle = restored.TextStyles.Should().Contain(s => s.Name == "Titel").Which;
        restoredStyle.FontFamily.Should().Be("Arial");
        restoredStyle.Height.Should().Be(22);
        restoredStyle.WidthFactor.Should().Be(1.1);
        restored.ActiveTextStyle.Id.Should().Be(style.Id);

        var restoredText = restored.Entities.OfType<TextEntity>().Should().ContainSingle().Subject;
        restoredText.TextStyleId.Should().Be(style.Id);
        restoredText.FontFamily.Should().Be("Arial");
        restoredText.WidthFactor.Should().Be(1.1);
    }
}
