using MiniCAD.Core.Entities;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Rendering;

/// <summary>
/// A transient entity drawn on top of the document with an explicit stroke, bypassing the
/// usual layer/override resolution. Used for tool previews (rubber-band geometry) and
/// selection highlights, neither of which belong to the document itself.
/// </summary>
public readonly record struct OverlayItem(IEntity Entity, StrokeStyle Stroke);
