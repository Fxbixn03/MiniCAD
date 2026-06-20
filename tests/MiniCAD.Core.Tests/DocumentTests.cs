using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class DocumentTests
{
    [Fact]
    public void NewDocument_HasDefaultLayerNamedZero()
    {
        var document = new CadDocument();

        document.Layers.Should().ContainSingle();
        document.DefaultLayer.Name.Should().Be("0");
        document.ActiveLayer.Should().Be(document.DefaultLayer);
    }

    [Fact]
    public void AddEntity_AssignsActiveLayer_AndRaisesEvent()
    {
        var document = new CadDocument();
        DocumentChangedEventArgs? captured = null;
        document.Changed += (_, e) => captured = e;

        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        document.AddEntity(line);

        line.LayerId.Should().Be(document.DefaultLayer.Id);
        captured.Should().NotBeNull();
        captured!.Kind.Should().Be(DocumentChangeKind.EntityAdded);
        captured.Entity.Should().BeSameAs(line);
    }

    [Fact]
    public void RemoveEntity_RaisesEvent_AndReturnsTrue()
    {
        var document = new CadDocument();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        document.AddEntity(line);

        DocumentChangeKind? kind = null;
        document.Changed += (_, e) => kind = e.Kind;

        document.RemoveEntity(line).Should().BeTrue();
        document.Entities.Should().BeEmpty();
        kind.Should().Be(DocumentChangeKind.EntityRemoved);
    }

    [Fact]
    public void ResolveStroke_PrefersOverride_ThenLayer()
    {
        var document = new CadDocument();
        Layer detail = document.AddLayer("Detail", new StrokeStyle(Color.Cyan, 2));

        var byLayer = new CircleEntity(Point2D.Origin, 1) { LayerId = detail.Id };
        var byOverride = new CircleEntity(Point2D.Origin, 1)
        {
            LayerId = detail.Id,
            StrokeOverride = new StrokeStyle(Color.Red, 3),
        };
        document.AddEntity(byLayer);
        document.AddEntity(byOverride);

        document.ResolveStroke(byLayer).Color.Should().Be(Color.Cyan);
        document.ResolveStroke(byOverride).Color.Should().Be(Color.Red);
    }

    [Fact]
    public void GetContentBounds_UnionsVisibleEntities()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(2, 2)));
        document.AddEntity(new LineEntity(new Point2D(-1, 5), new Point2D(4, 6)));

        Rect2D? bounds = document.GetContentBounds();

        bounds.Should().NotBeNull();
        bounds!.Value.Should().Be(new Rect2D(-1, 0, 4, 6));
    }

    [Fact]
    public void GetContentBounds_IgnoresHiddenLayers()
    {
        var document = new CadDocument();
        Layer hidden = document.AddLayer("Hidden", StrokeStyle.Default);
        document.SetLayerState(hidden, ElementState.Off);

        document.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(1, 1)));
        document.AddEntity(new LineEntity(new Point2D(100, 100), new Point2D(200, 200)) { LayerId = hidden.Id });

        document.GetContentBounds().Should().Be(new Rect2D(0, 0, 1, 1));
    }

    [Fact]
    public void RemoveLayer_RehomesEntitiesToDefault()
    {
        var document = new CadDocument();
        Layer temp = document.AddLayer("Temp", StrokeStyle.Default);
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = temp.Id };
        document.AddEntity(line);

        document.RemoveLayer(temp).Should().BeTrue();

        line.LayerId.Should().Be(document.DefaultLayer.Id);
    }

    [Fact]
    public void RemoveDefaultLayer_IsRejected()
    {
        var document = new CadDocument();

        document.RemoveLayer(document.DefaultLayer).Should().BeFalse();
    }

    [Fact]
    public void SetLayerColor_UpdatesStroke_KeepsWidth_AndRaisesModified()
    {
        var document = new CadDocument();
        Layer layer = document.AddLayer("Wände", new StrokeStyle(Color.White, 2.5));
        DocumentChangeKind? kind = null;
        document.Changed += (_, e) => kind = e.Kind;

        document.SetLayerColor(layer, Color.Red);

        layer.Stroke.Color.Should().Be(Color.Red);
        layer.Stroke.Width.Should().Be(2.5);
        kind.Should().Be(DocumentChangeKind.LayerModified);
    }

    [Fact]
    public void SetLayerColor_AffectsEntitiesUsingTheLayer()
    {
        var document = new CadDocument();
        Layer layer = document.AddLayer("Wände", new StrokeStyle(Color.White, 1));
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = layer.Id };
        document.AddEntity(line);

        document.SetLayerColor(layer, Color.Blue);

        document.ResolveStroke(line).Color.Should().Be(Color.Blue);
    }
}
