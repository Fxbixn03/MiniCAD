using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class PersistenceTests
{
    private static CadDocument BuildSampleDocument()
    {
        var document = new CadDocument();
        Layer details = document.AddLayer("Details", new StrokeStyle(Color.Cyan, 1.5));
        document.ActiveLayer = details;

        document.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(10, 5)));
        document.AddEntity(new CircleEntity(new Point2D(3, 3), 2.5)
        {
            LayerId = details.Id,
            StrokeOverride = new StrokeStyle(Color.Red, 3),
        });
        document.AddEntity(new ArcEntity(new Point2D(1, 1), 4, 0.5, 1.2));
        document.AddEntity(PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 3)));
        return document;
    }

    private static CadDocument RoundTrip(CadDocument source)
    {
        var fileDto = new ProjectFileDto { Name = "Test", Document = DocumentMapper.ToDto(source) };
        string json = ProjectSerializer.Serialize(fileDto);
        ProjectFileDto restoredDto = ProjectSerializer.Deserialize(json);

        var target = new CadDocument();
        DocumentMapper.Apply(restoredDto.Document, target);
        return target;
    }

    [Fact]
    public void RoundTrip_PreservesLayers_AndActiveDefault()
    {
        CadDocument source = BuildSampleDocument();

        CadDocument restored = RoundTrip(source);

        restored.Layers.Select(l => l.Id).Should().Equal(source.Layers.Select(l => l.Id));
        restored.DefaultLayer.Id.Should().Be(source.DefaultLayer.Id);
        restored.ActiveLayer.Id.Should().Be(source.ActiveLayer.Id);
        restored.ActiveLayer.Name.Should().Be("Details");
    }

    [Fact]
    public void RoundTrip_PreservesEntityCountAndTypes()
    {
        CadDocument restored = RoundTrip(BuildSampleDocument());

        restored.Entities.Should().HaveCount(4);
        restored.Entities.Select(e => e.GetType()).Should().Equal(
            typeof(LineEntity), typeof(CircleEntity), typeof(ArcEntity), typeof(PolylineEntity));
    }

    [Fact]
    public void RoundTrip_PreservesPolylineBulges()
    {
        var source = new CadDocument();
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) });
        poly.SetBulge(0, 0.5);
        source.AddEntity(poly);

        CadDocument restored = RoundTrip(source);

        var restoredPoly = restored.Entities.OfType<PolylineEntity>().Single();
        restoredPoly.GetBulge(0).Should().BeApproximately(0.5, 1e-9);
        restoredPoly.GetBulge(1).Should().Be(0.0);
    }

    [Fact]
    public void RoundTrip_PreservesSectionAndDetailMarks()
    {
        var source = new CadDocument();
        source.AddEntity(new SectionMarkEntity(
            new Point2D(0, 0), new Point2D(100, 0), "A", "Blatt 2", size: 30, flipDirection: true));
        source.AddEntity(new DetailMarkEntity(new Point2D(50, 50), 20, "1", "Detail D-04", textHeight: 8));

        CadDocument restored = RoundTrip(source);

        var section = restored.Entities.OfType<SectionMarkEntity>().Single();
        section.Start.Should().Be(new Point2D(0, 0));
        section.End.Should().Be(new Point2D(100, 0));
        section.Label.Should().Be("A");
        section.Reference.Should().Be("Blatt 2");
        section.Size.Should().BeApproximately(30, 1e-9);
        section.FlipDirection.Should().BeTrue();

        var detail = restored.Entities.OfType<DetailMarkEntity>().Single();
        detail.Center.Should().Be(new Point2D(50, 50));
        detail.Radius.Should().BeApproximately(20, 1e-9);
        detail.Label.Should().Be("1");
        detail.Reference.Should().Be("Detail D-04");
        detail.TextHeight.Should().BeApproximately(8, 1e-9);
    }

    [Fact]
    public void RoundTrip_PreservesGeometryAndStrokeOverride()
    {
        CadDocument restored = RoundTrip(BuildSampleDocument());

        var line = (LineEntity)restored.Entities[0];
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Should().Be(new Point2D(10, 5));

        var circle = (CircleEntity)restored.Entities[1];
        circle.Radius.Should().BeApproximately(2.5, 1e-9);
        circle.StrokeOverride.Should().NotBeNull();
        circle.StrokeOverride!.Value.Color.Should().Be(Color.Red);
        circle.StrokeOverride!.Value.Width.Should().Be(3);

        var poly = (PolylineEntity)restored.Entities[3];
        poly.IsClosed.Should().BeTrue();
        poly.Points.Should().HaveCount(4);
    }

    [Fact]
    public void RoundTrip_PreservesLayerColor()
    {
        var source = new CadDocument();
        source.AddLayer("Wände", new StrokeStyle(Color.Red, 2.0));

        CadDocument restored = RoundTrip(source);

        Layer restoredLayer = restored.Layers.Single(l => l.Name == "Wände");
        restoredLayer.Stroke.Color.Should().Be(Color.Red);
        restoredLayer.Stroke.Width.Should().Be(2.0);
    }

    [Fact]
    public void RoundTrip_PreservesLayerAndTeilbildState()
    {
        var source = new CadDocument();
        Layer layer = source.AddLayer("Referenz", StrokeStyle.Default);
        source.SetLayerState(layer, ElementState.Locked);
        source.SetPartialDrawingState(source.DefaultPartialDrawing, ElementState.Locked);

        CadDocument restored = RoundTrip(source);

        restored.FindLayer(layer.Id)!.State.Should().Be(ElementState.Locked);
        restored.PartialDrawings[0].State.Should().Be(ElementState.Locked);
    }

    [Fact]
    public void Load_LegacyFileWithoutState_DerivesStateFromVisibleLocked()
    {
        const string legacyJson = """
        {
          "version": 1, "name": "Legacy",
          "document": {
            "layers": [
              { "id": "11111111-1111-1111-1111-111111111111", "name": "Hidden",
                "stroke": { "color": { "r": 255, "g": 255, "b": 255, "a": 255 }, "width": 1 },
                "visible": false, "locked": false },
              { "id": "22222222-2222-2222-2222-222222222222", "name": "Locked",
                "stroke": { "color": { "r": 255, "g": 255, "b": 255, "a": 255 }, "width": 1 },
                "visible": true, "locked": true }
            ],
            "partialDrawings": [
              { "id": "33333333-3333-3333-3333-333333333333", "name": "TB", "visible": false }
            ],
            "entities": []
          }
        }
        """;

        ProjectFileDto dto = ProjectSerializer.Deserialize(legacyJson);
        var target = new CadDocument();
        DocumentMapper.Apply(dto.Document, target);

        target.FindLayer(Guid.Parse("11111111-1111-1111-1111-111111111111"))!.State.Should().Be(ElementState.Off);
        target.FindLayer(Guid.Parse("22222222-2222-2222-2222-222222222222"))!.State.Should().Be(ElementState.Locked);
        target.FindPartialDrawing(Guid.Parse("33333333-3333-3333-3333-333333333333"))!.State.Should().Be(ElementState.Off);
    }

    [Fact]
    public void RoundTrip_PreservesTeilbilder_AndEntityAssignment()
    {
        var source = new CadDocument();
        PartialDrawing detail = source.AddPartialDrawing("Detail");
        source.ActivePartialDrawing = detail;
        source.SetPartialDrawingState(detail, ElementState.Off);
        source.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(3, 3)));

        CadDocument restored = RoundTrip(source);

        restored.PartialDrawings.Select(p => p.Id).Should().Equal(source.PartialDrawings.Select(p => p.Id));
        restored.ActivePartialDrawing.Id.Should().Be(detail.Id);
        restored.FindPartialDrawing(detail.Id)!.State.Should().Be(ElementState.Off);
        restored.Entities[0].PartialDrawingId.Should().Be(detail.Id);
    }

    [Fact]
    public void Apply_LegacyDocumentWithoutTeilbilder_AdoptsEntitiesIntoDefault()
    {
        // Simulate an old project file: entities, but no partial drawings.
        DocumentDto legacy = DocumentMapper.ToDto(BuildSampleDocument());
        legacy.PartialDrawings.Clear();
        legacy.ActivePartialDrawingId = Guid.Empty;
        foreach (EntityDto entity in legacy.Entities)
            entity.PartialDrawingId = Guid.Empty;

        var target = new CadDocument();
        DocumentMapper.Apply(legacy, target);

        target.PartialDrawings.Should().ContainSingle();
        Guid defaultId = target.PartialDrawings[0].Id;
        target.Entities.Should().OnlyContain(e => e.PartialDrawingId == defaultId);
    }

    [Fact]
    public void RoundTrip_PreservesEntityLayerAssignment()
    {
        CadDocument source = BuildSampleDocument();
        Guid detailsLayerId = source.ActiveLayer.Id;

        CadDocument restored = RoundTrip(source);

        // The circle was explicitly placed on the "Details" layer.
        restored.Entities[1].LayerId.Should().Be(detailsLayerId);
        restored.FindLayer(detailsLayerId).Should().NotBeNull();
    }

    [Fact]
    public void SaveAndLoad_File_RoundTripsThroughDisk()
    {
        CadDocument source = BuildSampleDocument();
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"minicad_test_{Guid.NewGuid():N}{ProjectSerializer.FileExtension}");

        try
        {
            ProjectSerializer.Save(new ProjectFileDto { Name = "Disk", Document = DocumentMapper.ToDto(source) }, path);
            ProjectFileDto loaded = ProjectSerializer.Load(path);

            loaded.Name.Should().Be("Disk");
            var target = new CadDocument();
            DocumentMapper.Apply(loaded.Document, target);
            target.Entities.Should().HaveCount(source.Entities.Count);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void LoadContents_ReplacesDocument_AndRaisesReloaded()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)));

        DocumentChangeKind? kind = null;
        document.Changed += (_, e) => kind = e.Kind;

        DocumentMapper.Apply(DocumentMapper.ToDto(BuildSampleDocument()), document);

        kind.Should().Be(DocumentChangeKind.Reloaded);
        document.Entities.Should().HaveCount(4);
    }
}
