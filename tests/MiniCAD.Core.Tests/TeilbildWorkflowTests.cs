using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class TeilbildWorkflowTests
{
    [Fact]
    public void Color_Lerp_BlendsChannels()
    {
        Color.White.Lerp(Color.Black, 0.0).Should().Be(Color.White);
        Color.White.Lerp(Color.Black, 1.0).Should().Be(Color.Black);

        Color mid = Color.White.Lerp(Color.Black, 0.5);
        mid.R.Should().BeInRange(127, 128);
        mid.G.Should().BeInRange(127, 128);
        mid.B.Should().BeInRange(127, 128);
    }

    [Fact]
    public void DefaultTeilbild_IsTheCurrentOne()
    {
        var doc = new CadDocument();

        doc.GetPartialDrawingStatus(doc.ActivePartialDrawing).Should().Be(PartialDrawingStatus.Current);
    }

    [Fact]
    public void Status_ReflectsStateAndCurrentFlag()
    {
        var doc = new CadDocument();
        PartialDrawing background = doc.AddPartialDrawing("Hintergrund");

        // Active but not the current target.
        doc.GetPartialDrawingStatus(background).Should().Be(PartialDrawingStatus.Active);

        doc.SetPartialDrawingState(background, ElementState.Locked);
        doc.GetPartialDrawingStatus(background).Should().Be(PartialDrawingStatus.Passive);

        doc.SetPartialDrawingState(background, ElementState.Off);
        doc.GetPartialDrawingStatus(background).Should().Be(PartialDrawingStatus.Off);
    }

    [Fact]
    public void MultipleActiveTeilbilder_AreAllEditable()
    {
        var doc = new CadDocument();
        PartialDrawing second = doc.AddPartialDrawing("Zweites"); // Active by default

        doc.ActivePartialDrawing.IsEditable.Should().BeTrue();
        second.IsEditable.Should().BeTrue(); // active-in-background is still editable
    }

    [Fact]
    public void SetCurrent_ForcesActive_SetsTargetAndNotifies()
    {
        var doc = new CadDocument();
        PartialDrawing passive = doc.AddPartialDrawing("Passiv");
        doc.SetPartialDrawingState(passive, ElementState.Locked);

        var raised = new List<DocumentChangeKind>();
        doc.Changed += (_, e) => raised.Add(e.Kind);

        doc.SetCurrentPartialDrawing(passive);

        doc.ActivePartialDrawing.Should().Be(passive);
        passive.State.Should().Be(ElementState.Active);          // forced editable
        doc.GetPartialDrawingStatus(passive).Should().Be(PartialDrawingStatus.Current);
        raised.Should().Contain(DocumentChangeKind.PartialDrawingModified);
    }

    [Fact]
    public void ReferenceScale_DefaultsTo100_AndSurvivesPersistence()
    {
        var doc = new CadDocument();
        doc.ActivePartialDrawing.ReferenceScale.Should().Be(100);
        doc.ActivePartialDrawing.ModelScaleFactor.Should().Be(1.0);

        doc.SetPartialDrawingReferenceScale(doc.ActivePartialDrawing, 50);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        restored.ActivePartialDrawing.ReferenceScale.Should().Be(50);
        restored.ActivePartialDrawing.ModelScaleFactor.Should().Be(0.5);
    }

    [Fact]
    public void HatchGeometry_ScalesSpacing_WithReferenceScale()
    {
        var square = new[]
        {
            new Point2D(0, 0), new Point2D(100, 0), new Point2D(100, 100), new Point2D(0, 100),
        };
        var pattern = new HatchPattern("test", new[] { new HatchLineDefinition(0, 10) }, Color.White);

        int dense = HatchGeometry.Generate(square, pattern, spacingScale: 1.0).Count;
        int coarse = HatchGeometry.Generate(square, pattern, spacingScale: 2.0).Count; // spacing 20
        int denser = HatchGeometry.Generate(square, pattern, spacingScale: 0.5).Count; // spacing 5

        coarse.Should().BeLessThan(dense);   // wider spacing -> fewer lines
        denser.Should().BeGreaterThan(dense); // tighter spacing -> more lines
    }
}
