using FluentAssertions;
using MiniCAD.Core.Documents;
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
}
