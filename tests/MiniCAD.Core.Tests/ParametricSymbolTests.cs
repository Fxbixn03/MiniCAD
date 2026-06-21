using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;

namespace MiniCAD.Core.Tests;

public class ParametricSymbolTests
{
    [Fact]
    public void Catalog_HasBuiltInSymbols_WithDefaults()
    {
        ParametricSymbolCatalog.All.Select(d => d.Key).Should().Contain(new[] { "rect", "window", "door" });
        ParametricSymbolCatalog.Defaults("rect").Should().ContainKey("Breite").And.ContainKey("Höhe");
    }

    [Fact]
    public void ChangingAParameter_RegeneratesGeometry()
    {
        var symbol = new ParametricSymbolEntity("rect", Point2D.Origin);
        double widthAt100 = symbol.Bounds.Width;
        widthAt100.Should().BeApproximately(100, 1e-6); // default Breite

        symbol.Parameters["Breite"] = 250;
        symbol.Bounds.Width.Should().BeApproximately(250, 1e-6); // regenerated
    }

    [Fact]
    public void Scale_AppliesToGeneratedGeometry()
    {
        var symbol = new ParametricSymbolEntity("rect", Point2D.Origin,
            new Dictionary<string, double> { ["Breite"] = 100, ["Höhe"] = 50 }, scale: 2.0);

        symbol.Bounds.Width.Should().BeApproximately(200, 1e-6);
    }

    [Fact]
    public void ParametricSymbol_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new ParametricSymbolEntity("door", new Point2D(5, 5),
            new Dictionary<string, double> { ["Breite"] = 110 }, scale: 1.5, rotation: 0.3));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var symbol = restored.Entities.OfType<ParametricSymbolEntity>().Should().ContainSingle().Subject;
        symbol.SymbolKey.Should().Be("door");
        symbol.Parameters["Breite"].Should().Be(110);
        symbol.Scale.Should().Be(1.5);
        symbol.Rotation.Should().Be(0.3);
    }
}
