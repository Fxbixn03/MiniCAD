using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Materials;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class MaterialLibraryTests
{
    [Fact]
    public void Default_ContainsTheCoreBuildingMaterials()
    {
        MaterialLibrary lib = MaterialLibrary.Default;
        lib.TryGet("Stahlbeton", out MaterialDefinition concrete).Should().BeTrue();
        concrete.Category.Should().Be(MaterialCategory.Concrete);
        concrete.Density.Should().BeGreaterThan(0);

        lib.GetByCategory(MaterialCategory.Masonry).Should().NotBeEmpty();
        lib.GetByCategory(MaterialCategory.Glass).Should().Contain(m => m.Opacity < 1.0f);
    }

    [Fact]
    public void Add_ReplacesByNameCaseInsensitive()
    {
        var lib = new MaterialLibrary();
        lib.Add(new MaterialDefinition("Test", MaterialCategory.Generic, new Color(10, 20, 30)));
        lib.Add(new MaterialDefinition("test", MaterialCategory.Wood, new Color(40, 50, 60)));

        lib.Materials.Should().ContainSingle();
        lib.TryGet("TEST", out MaterialDefinition m).Should().BeTrue();
        m.Category.Should().Be(MaterialCategory.Wood);
    }

    [Fact]
    public void RoundTripsThroughJson_McmatFormat()
    {
        MaterialLibrary lib = MaterialLibrary.Default;
        string json = lib.ToJson();
        MaterialLibrary restored = MaterialLibrary.FromJson(json);

        restored.Materials.Should().HaveCount(lib.Materials.Count);
        restored.TryGet("Floatglas", out MaterialDefinition glass).Should().BeTrue();
        glass.DiffuseColor.Should().Be(new Color(0xB0, 0xC4, 0xDE));
        glass.Opacity.Should().BeApproximately(0.35f, 1e-4f);
    }

    [Fact]
    public void TryGet_ReturnsFalseAndConcreteFallback_WhenUnknown()
    {
        MaterialLibrary.Default.TryGet("Unobtanium", out MaterialDefinition fallback).Should().BeFalse();
        fallback.Name.Should().Be("Stahlbeton");
    }
}
