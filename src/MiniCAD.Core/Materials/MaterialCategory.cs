namespace MiniCAD.Core.Materials;

/// <summary>Construction-relevant material groups for the material library (#275).</summary>
public enum MaterialCategory
{
    Concrete,   // Beton, Stahlbeton
    Masonry,    // Mauerwerk, Ziegel
    Wood,       // Holz, Brettsperrholz
    Steel,      // Stahl, Metall
    Glass,      // Glas, Verglasung
    Insulation, // Dämmung
    Earth,      // Erdreich, Boden
    Generic,    // Sonstige
}
