using System.Linq;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Persistence;

/// <summary>
/// Maps between the domain model and the persistence DTOs. This is the single place that
/// knows how each entity type is represented on disk, keeping serialization concerns out of
/// the entity classes themselves.
/// </summary>
public static class DocumentMapper
{
    public static DocumentDto ToDto(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var dto = new DocumentDto
        {
            DefaultLayerId = document.DefaultLayer.Id,
            ActiveLayerId = document.ActiveLayer.Id,
            ActivePartialDrawingId = document.ActivePartialDrawing.Id,
            DefaultTextStyleId = document.DefaultTextStyle.Id,
            ActiveTextStyleId = document.ActiveTextStyle.Id,
            OriginX = document.CoordinateSystem.Origin.X,
            OriginY = document.CoordinateSystem.Origin.Y,
            OriginZ = document.CoordinateSystem.Origin.Z,
        };

        foreach (Layer layer in document.Layers)
            dto.Layers.Add(ToDto(layer));

        foreach (TextStyle style in document.TextStyles)
            dto.TextStyles.Add(ToDto(style));

        foreach (PartialDrawing partialDrawing in document.PartialDrawings)
            dto.PartialDrawings.Add(ToDto(partialDrawing));

        foreach (HatchPattern pattern in document.Patterns)
            dto.Patterns.Add(ToDto(pattern));

        foreach (IEntity entity in document.Entities)
            dto.Entities.Add(ToDto(entity));

        return dto;
    }

    /// <summary>Loads a DTO into an existing document in place.</summary>
    public static void Apply(DocumentDto dto, CadDocument target)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(target);

        List<Layer> layers = dto.Layers.Select(FromDto).ToList();
        if (layers.Count == 0)
            layers.Add(new Layer("0", new StrokeStyle(Color.White, 1.0)));

        // Older files have no Teilbilder; create a default one and adopt all entities into it.
        List<PartialDrawing> partialDrawings = dto.PartialDrawings.Select(FromDto).ToList();
        if (partialDrawings.Count == 0)
            partialDrawings.Add(new PartialDrawing("Teilbild 1"));
        Guid fallbackPartialDrawingId = partialDrawings[0].Id;

        List<HatchPattern> patterns = dto.Patterns.Select(FromDto).ToList();

        List<TextStyle> textStyles = dto.TextStyles.Select(FromDto).ToList();

        List<IEntity> entities = dto.Entities.Select(FromDto).ToList();
        foreach (IEntity entity in entities)
        {
            if (entity.PartialDrawingId == Guid.Empty)
                entity.PartialDrawingId = fallbackPartialDrawingId;
        }

        // Resolve polyline fills against the just-loaded project patterns and the global library.
        ResolveFills(dto.Entities, entities, patterns);

        Guid activePartialDrawingId = dto.ActivePartialDrawingId != Guid.Empty
            ? dto.ActivePartialDrawingId
            : fallbackPartialDrawingId;

        target.LoadContents(new DocumentContents(
            layers, dto.DefaultLayerId, dto.ActiveLayerId,
            partialDrawings, activePartialDrawingId, entities)
        {
            Patterns = patterns,
            TextStyles = textStyles,
            DefaultTextStyleId = dto.DefaultTextStyleId,
            ActiveTextStyleId = dto.ActiveTextStyleId,
            Origin = new Point3D(dto.OriginX, dto.OriginY, dto.OriginZ),
        });
    }

    private static void ResolveFills(IReadOnlyList<EntityDto> dtos, IReadOnlyList<IEntity> entities, IReadOnlyList<HatchPattern> projectPatterns)
    {
        for (int i = 0; i < dtos.Count; i++)
        {
            if (dtos[i] is PolylineDto { FillPatternId: { } fillId } && entities[i] is PolylineEntity poly)
            {
                poly.Fill = projectPatterns.FirstOrDefault(p => p.Id == fillId) ?? PatternLibrary.Find(fillId);
            }
        }
    }

    private static PatternDto ToDto(HatchPattern pattern) => new()
    {
        Id = pattern.Id,
        Name = pattern.Name,
        Color = ToDto(pattern.Color),
        LineWidth = pattern.LineWidth,
        Lines = pattern.Lines.Select(l => new HatchLineDto
        {
            AngleDegrees = l.AngleDegrees,
            Spacing = l.Spacing,
            Offset = l.Offset,
        }).ToList(),
    };

    private static HatchPattern FromDto(PatternDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Lines.Select(l => new HatchLineDefinition(l.AngleDegrees, l.Spacing, l.Offset)).ToList(),
        FromDto(dto.Color),
        dto.LineWidth);

    private static TextStyleDto ToDto(TextStyle style) => new()
    {
        Id = style.Id,
        Name = style.Name,
        FontFamily = style.FontFamily,
        Height = style.Height,
        WidthFactor = style.WidthFactor,
    };

    private static TextStyle FromDto(TextStyleDto dto)
        => new(dto.Id, dto.Name, dto.FontFamily, dto.Height, dto.WidthFactor);

    private static LayerDto ToDto(Layer layer) => new()
    {
        Id = layer.Id,
        Name = layer.Name,
        Stroke = ToDto(layer.Stroke),
        State = layer.State,
    };

    private static Layer FromDto(LayerDto dto) => new(dto.Id, dto.Name, FromDto(dto.Stroke))
    {
        State = dto.State ?? DeriveState(dto.Visible, dto.Locked),
    };

    private static PartialDrawingDto ToDto(PartialDrawing partialDrawing) => new()
    {
        Id = partialDrawing.Id,
        Name = partialDrawing.Name,
        State = partialDrawing.State,
        ReferenceScale = partialDrawing.ReferenceScale,
    };

    private static PartialDrawing FromDto(PartialDrawingDto dto) => new(dto.Id, dto.Name)
    {
        State = dto.State ?? DeriveState(dto.Visible, locked: false),
        ReferenceScale = dto.ReferenceScale,
    };

    // Maps legacy visible/locked flags onto the three-state model for older files.
    private static ElementState DeriveState(bool? visible, bool? locked)
    {
        if (visible == false)
            return ElementState.Off;
        return locked == true ? ElementState.Locked : ElementState.Active;
    }

    private static StrokeDto ToDto(StrokeStyle stroke) => new()
    {
        Color = ToDto(stroke.Color),
        Width = stroke.Width,
        LineType = stroke.LineType.ToString(),
        LineWeightMm = stroke.LineWeightMm,
    };

    private static StrokeStyle FromDto(StrokeDto dto) => new(
        FromDto(dto.Color),
        dto.Width,
        Enum.TryParse(dto.LineType, out LineType lineType) ? lineType : LineType.Solid,
        dto.LineWeightMm);

    private static ColorDto ToDto(Color color) => new() { R = color.R, G = color.G, B = color.B, A = color.A };

    private static Color FromDto(ColorDto dto) => new(dto.R, dto.G, dto.B, dto.A);

    private static PointDto ToDto(Point2D point) => new() { X = point.X, Y = point.Y };

    private static Point2D FromDto(PointDto dto) => new(dto.X, dto.Y);

    private static EntityDto ToDto(IEntity entity)
    {
        EntityDto dto = entity switch
        {
            LineEntity line => new LineDto { Start = ToDto(line.Start), End = ToDto(line.End) },
            CircleEntity circle => new CircleDto { Center = ToDto(circle.Center), Radius = circle.Radius },
            ArcEntity arc => new ArcDto
            {
                Center = ToDto(arc.Center),
                Radius = arc.Radius,
                StartAngle = arc.StartAngle,
                SweepAngle = arc.SweepAngle,
            },
            PolylineEntity poly => new PolylineDto
            {
                Points = poly.Points.Select(ToDto).ToList(),
                Closed = poly.IsClosed,
                FillPatternId = poly.Fill?.Id,
            },
            PointEntity point => new PointMarkerDto
            {
                Position = ToDto(point.Position),
                Size = point.Size,
                Style = point.Style.ToString(),
            },
            EllipseEntity ellipse => new EllipseDto
            {
                Center = ToDto(ellipse.Center),
                RadiusX = ellipse.RadiusX,
                RadiusY = ellipse.RadiusY,
                Rotation = ellipse.Rotation,
                StartAngle = ellipse.StartAngle,
                SweepAngle = ellipse.SweepAngle,
            },
            SplineEntity spline => new SplineDto { Points = spline.Points.Select(ToDto).ToList() },
            TextEntity text => new TextDto
            {
                Position = ToDto(text.Position),
                Text = text.Text,
                Height = text.Height,
                Rotation = text.Rotation,
                HAlign = text.HorizontalAlignment.ToString(),
                VAlign = text.VerticalAlignment.ToString(),
                TextStyleId = text.TextStyleId,
                FontFamily = text.FontFamily,
                WidthFactor = text.WidthFactor,
            },
            MTextEntity mtext => new MTextDto
            {
                Position = ToDto(mtext.Position),
                Text = mtext.Text,
                Height = mtext.Height,
                Width = mtext.Width,
                Rotation = mtext.Rotation,
                HAlign = mtext.HorizontalAlignment.ToString(),
                VAlign = mtext.VerticalAlignment.ToString(),
                TextStyleId = mtext.TextStyleId,
                FontFamily = mtext.FontFamily,
                WidthFactor = mtext.WidthFactor,
            },
            LeaderEntity leader => new LeaderDto
            {
                Points = leader.Points.Select(ToDto).ToList(),
                Text = leader.Text,
                TextHeight = leader.TextHeight,
                ArrowSize = leader.ArrowSize,
            },
            _ => throw new NotSupportedException($"Entity type '{entity.GetType().Name}' cannot be serialized."),
        };

        dto.LayerId = entity.LayerId;
        dto.PartialDrawingId = entity.PartialDrawingId;
        dto.Stroke = entity.StrokeOverride is { } stroke ? ToDto(stroke) : null;
        return dto;
    }

    private static IEntity FromDto(EntityDto dto)
    {
        IEntity entity = dto switch
        {
            LineDto line => new LineEntity(FromDto(line.Start), FromDto(line.End)),
            CircleDto circle => new CircleEntity(FromDto(circle.Center), circle.Radius),
            ArcDto arc => new ArcEntity(FromDto(arc.Center), arc.Radius, arc.StartAngle, arc.SweepAngle),
            PolylineDto poly => new PolylineEntity(poly.Points.Select(FromDto), poly.Closed),
            PointMarkerDto point => new PointEntity(
                FromDto(point.Position),
                point.Size,
                Enum.TryParse(point.Style, out PointStyle style) ? style : PointStyle.Plus),
            EllipseDto ellipse => new EllipseEntity(
                FromDto(ellipse.Center), ellipse.RadiusX, ellipse.RadiusY,
                ellipse.Rotation, ellipse.StartAngle, ellipse.SweepAngle),
            SplineDto spline => new SplineEntity(spline.Points.Select(FromDto)),
            TextDto text => new TextEntity(
                FromDto(text.Position), text.Text, text.Height, text.Rotation,
                ParseHAlign(text.HAlign), ParseVAlign(text.VAlign))
            {
                TextStyleId = text.TextStyleId,
                FontFamily = text.FontFamily,
                WidthFactor = text.WidthFactor,
            },
            MTextDto mtext => new MTextEntity(
                FromDto(mtext.Position), mtext.Text, mtext.Height, mtext.Width, mtext.Rotation,
                ParseHAlign(mtext.HAlign), ParseVAlign(mtext.VAlign))
            {
                TextStyleId = mtext.TextStyleId,
                FontFamily = mtext.FontFamily,
                WidthFactor = mtext.WidthFactor,
            },
            LeaderDto leader => new LeaderEntity(
                leader.Points.Select(FromDto), leader.Text, leader.TextHeight, leader.ArrowSize),
            _ => throw new NotSupportedException($"Entity DTO '{dto.GetType().Name}' cannot be deserialized."),
        };

        entity.LayerId = dto.LayerId;
        entity.PartialDrawingId = dto.PartialDrawingId;
        entity.StrokeOverride = dto.Stroke is { } stroke ? FromDto(stroke) : null;
        return entity;
    }

    private static TextHAlign ParseHAlign(string value)
        => Enum.TryParse(value, out TextHAlign align) ? align : TextHAlign.Left;

    private static TextVAlign ParseVAlign(string value)
        => Enum.TryParse(value, out TextVAlign align) ? align : TextVAlign.Baseline;
}
