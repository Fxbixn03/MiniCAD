using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class LayerFavoriteTests
{
    [Fact]
    public void SaveAndApplyLayerFavorite_RestoresLayerStates()
    {
        var doc = new CadDocument();
        Layer a = doc.AddLayer("Wände", new StrokeStyle(Color.White));
        Layer b = doc.AddLayer("Möbel", new StrokeStyle(Color.White));

        doc.SetLayerState(a, ElementState.Active);
        doc.SetLayerState(b, ElementState.Off);
        LayerFavorite favorite = doc.SaveLayerFavorite("Rohbau");

        // Change states, then restore from the favorite.
        doc.SetLayerState(a, ElementState.Off);
        doc.SetLayerState(b, ElementState.Active);
        doc.ApplyLayerFavorite(favorite);

        a.State.Should().Be(ElementState.Active);
        b.State.Should().Be(ElementState.Off);
    }

    [Fact]
    public void RemoveLayerFavorite_RemovesIt()
    {
        var doc = new CadDocument();
        LayerFavorite favorite = doc.SaveLayerFavorite("Temp");
        doc.LayerFavorites.Should().ContainSingle();

        doc.RemoveLayerFavorite(favorite).Should().BeTrue();
        doc.LayerFavorites.Should().BeEmpty();
    }

    [Fact]
    public void LayerFavorites_SurvivePersistenceRoundTrip()
    {
        var doc = new CadDocument();
        Layer a = doc.AddLayer("Wände", new StrokeStyle(Color.White));
        doc.SetLayerState(a, ElementState.Locked);
        doc.SaveLayerFavorite("Plan-Ansicht");

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        LayerFavorite favorite = restored.LayerFavorites.Should().ContainSingle().Subject;
        favorite.Name.Should().Be("Plan-Ansicht");
        favorite.States[a.Id].Should().Be(ElementState.Locked);
    }
}
