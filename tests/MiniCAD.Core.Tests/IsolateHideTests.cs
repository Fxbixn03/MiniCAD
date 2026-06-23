using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class IsolateHideTests
{
    private static (CadDocument doc, LineEntity a, LineEntity b, LineEntity c) Setup()
    {
        var doc = new CadDocument();
        var a = new LineEntity(Point2D.Origin, new Point2D(1, 0));
        var b = new LineEntity(Point2D.Origin, new Point2D(0, 1));
        var c = new LineEntity(Point2D.Origin, new Point2D(1, 1));
        doc.AddEntity(a);
        doc.AddEntity(b);
        doc.AddEntity(c);
        return (doc, a, b, c);
    }

    [Fact]
    public void HideEntities_MakesThemInvisibleAndNonEditable()
    {
        var (doc, a, b, _) = Setup();

        doc.HideEntities(new[] { a });

        doc.IsEntityHidden(a).Should().BeTrue();
        doc.IsEntityVisible(a).Should().BeFalse();
        doc.IsEntityEditable(a).Should().BeFalse();
        doc.IsEntityVisible(b).Should().BeTrue();
        doc.HasHiddenEntities.Should().BeTrue();
    }

    [Fact]
    public void IsolateEntities_HidesEverythingElse()
    {
        var (doc, a, b, c) = Setup();

        doc.IsolateEntities(new[] { a });

        doc.IsEntityVisible(a).Should().BeTrue();
        doc.IsEntityVisible(b).Should().BeFalse();
        doc.IsEntityVisible(c).Should().BeFalse();
    }

    [Fact]
    public void ShowAllEntities_ClearsHiddenSet()
    {
        var (doc, a, _, _) = Setup();
        doc.HideEntities(new[] { a });

        doc.ShowAllEntities();

        doc.HasHiddenEntities.Should().BeFalse();
        doc.IsEntityVisible(a).Should().BeTrue();
    }

    [Fact]
    public void HideEntities_RaisesVisibilityChanged()
    {
        var (doc, a, _, _) = Setup();
        DocumentChangeKind? kind = null;
        doc.Changed += (_, e) => kind = e.Kind;

        doc.HideEntities(new[] { a });

        kind.Should().Be(DocumentChangeKind.VisibilityChanged);
    }

    [Fact]
    public void HiddenEntities_AreExcludedFromContentBounds()
    {
        var doc = new CadDocument();
        var visible = new LineEntity(new Point2D(0, 0), new Point2D(2, 2));
        var far = new LineEntity(new Point2D(100, 100), new Point2D(110, 110));
        doc.AddEntity(visible);
        doc.AddEntity(far);

        doc.HideEntities(new[] { far });
        Rect2D bounds = doc.GetContentBounds()!.Value;

        bounds.MaxX.Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void Clear_ResetsHiddenSet()
    {
        var (doc, a, _, _) = Setup();
        doc.HideEntities(new[] { a });

        doc.Clear();

        doc.HasHiddenEntities.Should().BeFalse();
    }
}
