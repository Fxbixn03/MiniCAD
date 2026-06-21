using System.Linq;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
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
            DefaultDimStyleId = document.DefaultDimStyle.Id,
            ActiveDimStyleId = document.ActiveDimStyle.Id,
            OriginX = document.CoordinateSystem.Origin.X,
            OriginY = document.CoordinateSystem.Origin.Y,
            OriginZ = document.CoordinateSystem.Origin.Z,
        };

        foreach (Layer layer in document.Layers)
            dto.Layers.Add(ToDto(layer));

        foreach (TextStyle style in document.TextStyles)
            dto.TextStyles.Add(ToDto(style));

        foreach (DimStyle style in document.DimStyles)
            dto.DimStyles.Add(ToDto(style));

        foreach (LayerFavorite favorite in document.LayerFavorites)
            dto.LayerFavorites.Add(ToDto(favorite));

        foreach (BlockDefinition definition in document.BlockDefinitions)
            dto.BlockDefinitions.Add(ToDto(definition));

        foreach (Model3DObject model in document.Models)
            dto.Models.Add(ToDto(model));

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

        List<DimStyle> dimStyles = dto.DimStyles.Select(FromDto).ToList();

        List<LayerFavorite> layerFavorites = dto.LayerFavorites.Select(FromDto).ToList();

        List<BlockDefinition> blockDefinitions = dto.BlockDefinitions.Select(FromDto).ToList();

        List<Model3DObject> models = dto.Models.Select(FromDto).ToList();

        List<IEntity> entities = dto.Entities.Select(FromDto).ToList();

        // Link block references to their definitions (only the id is persisted).
        foreach (IEntity entity in entities)
        {
            if (entity is BlockReferenceEntity reference)
                reference.Definition = blockDefinitions.FirstOrDefault(d => d.Id == reference.DefinitionId);
        }
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
            DimStyles = dimStyles,
            DefaultDimStyleId = dto.DefaultDimStyleId,
            ActiveDimStyleId = dto.ActiveDimStyleId,
            LayerFavorites = layerFavorites,
            BlockDefinitions = blockDefinitions,
            Models = models,
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

    private static DimStyleDto ToDto(DimStyle style) => new()
    {
        Id = style.Id,
        Name = style.Name,
        TextHeight = style.TextHeight,
        ArrowSize = style.ArrowSize,
        ExtensionOffset = style.ExtensionOffset,
        ExtensionOvershoot = style.ExtensionOvershoot,
        DecimalPlaces = style.DecimalPlaces,
    };

    private static DimStyle FromDto(DimStyleDto dto) => new(
        dto.Id, dto.Name, dto.TextHeight, dto.ArrowSize, dto.ExtensionOffset, dto.ExtensionOvershoot, dto.DecimalPlaces);

    private static LayerFavoriteDto ToDto(LayerFavorite favorite) => new()
    {
        Id = favorite.Id,
        Name = favorite.Name,
        States = favorite.States.Select(kv => new LayerStateEntryDto { LayerId = kv.Key, State = kv.Value }).ToList(),
    };

    private static LayerFavorite FromDto(LayerFavoriteDto dto) => new(
        dto.Id, dto.Name, dto.States.ToDictionary(e => e.LayerId, e => e.State));

    /// <summary>Maps a block definition to its DTO (used for the cross-project symbol library).</summary>
    public static BlockDefinitionDto ToDto(BlockDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        BasePoint = ToDto(definition.BasePoint),
        Entities = definition.Entities.Select(ToDto).ToList(),
        AttributeKeys = definition.AttributeKeys.ToList(),
    };

    /// <summary>Maps a block-definition DTO back to a definition (symbol library).</summary>
    public static BlockDefinition FromDto(BlockDefinitionDto dto)
    {
        var definition = new BlockDefinition(dto.Id, dto.Name, FromDto(dto.BasePoint), dto.Entities.Select(FromDto));
        definition.AttributeKeys.AddRange(dto.AttributeKeys);
        return definition;
    }

    /// <summary>Copies the shared dimension fields onto the persisted DTO and returns it.</summary>
    private static T FillDimDto<T>(T dto, DimensionEntity dim) where T : DimensionDto
    {
        dto.DimStyleId = dim.DimStyleId;
        dto.TextHeight = dim.TextHeight;
        dto.ArrowSize = dim.ArrowSize;
        dto.ExtensionOffset = dim.ExtensionOffset;
        dto.ExtensionOvershoot = dim.ExtensionOvershoot;
        dto.DecimalPlaces = dim.DecimalPlaces;
        dto.TextOverride = dim.TextOverride;
        return dto;
    }

    /// <summary>Restores the shared dimension fields from the DTO onto the entity.</summary>
    private static T FillDim<T>(T dim, DimensionDto dto) where T : DimensionEntity
    {
        dim.DimStyleId = dto.DimStyleId;
        dim.TextHeight = dto.TextHeight;
        dim.ArrowSize = dto.ArrowSize;
        dim.ExtensionOffset = dto.ExtensionOffset;
        dim.ExtensionOvershoot = dto.ExtensionOvershoot;
        dim.DecimalPlaces = dto.DecimalPlaces;
        dim.TextOverride = dto.TextOverride;
        return dim;
    }

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
        BaseHeight = partialDrawing.BaseHeight,
        Height = partialDrawing.Height,
    };

    private static PartialDrawing FromDto(PartialDrawingDto dto) => new(dto.Id, dto.Name)
    {
        State = dto.State ?? DeriveState(dto.Visible, locked: false),
        ReferenceScale = dto.ReferenceScale,
        BaseHeight = dto.BaseHeight,
        Height = dto.Height,
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

    private static Point3DDto ToDto(Point3D point) => new() { X = point.X, Y = point.Y, Z = point.Z };

    private static Point3D FromDto(Point3DDto dto) => new(dto.X, dto.Y, dto.Z);

    private static Model3DDto ToDto(Model3DObject model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Mesh = new Mesh3DDto
        {
            Vertices = model.Mesh.Vertices.Select(ToDto).ToList(),
            Indices = model.Mesh.Indices.ToList(),
        },
        Transform = model.Transform.ToArray().ToList(),
        Color = ToDto(model.Color),
    };

    private static Model3DObject FromDto(Model3DDto dto) => new(
        dto.Id, dto.Name,
        new Mesh3D(dto.Mesh.Vertices.Select(FromDto), dto.Mesh.Indices),
        Matrix4.FromArray(dto.Transform),
        FromDto(dto.Color));

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
                Bulges = poly.Bulges.Any(b => b != 0.0) ? poly.Bulges.ToList() : new List<double>(),
                Closed = poly.IsClosed,
                FillPatternId = poly.Fill?.Id,
                Filled = poly.SolidFill.HasValue,
                FillColor = ToDto((poly.SolidFill ?? default).Color),
                FillColor2 = ToDto((poly.SolidFill ?? default).SecondColor),
                FillGradient = poly.SolidFill?.IsGradient ?? false,
                FillAngle = poly.SolidFill?.AngleDegrees ?? 0.0,
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
            ImageEntity image => new ImageDto
            {
                Data = Convert.ToBase64String(image.Data),
                Origin = ToDto(image.Origin),
                Width = image.Width,
                Height = image.Height,
                Rotation = image.Rotation,
            },
            LinearDimensionEntity dim => FillDimDto(new LinearDimensionDto
            {
                P1 = ToDto(dim.P1),
                P2 = ToDto(dim.P2),
                DimLinePoint = ToDto(dim.DimLinePoint),
                Orientation = dim.Kind.ToString(),
            }, dim),
            AngularDimensionEntity dim => FillDimDto(new AngularDimensionDto
            {
                Vertex = ToDto(dim.Vertex),
                P1 = ToDto(dim.P1),
                P2 = ToDto(dim.P2),
                ArcPoint = ToDto(dim.ArcPoint),
            }, dim),
            RadialDimensionEntity dim => FillDimDto(new RadialDimensionDto
            {
                Center = ToDto(dim.Center),
                EdgePoint = ToDto(dim.EdgePoint),
                TextPoint = ToDto(dim.TextPoint),
                IsDiameter = dim.IsDiameter,
            }, dim),
            ElevationDimensionEntity dim => FillDimDto(new ElevationDimensionDto
            {
                Position = ToDto(dim.Position),
                ZValue = dim.ZValue,
            }, dim),
            OrdinateDimensionEntity dim => FillDimDto(new OrdinateDimensionDto
            {
                Position = ToDto(dim.Position),
                LeaderEnd = ToDto(dim.LeaderEnd),
                Origin = ToDto(dim.Origin),
            }, dim),
            BlockReferenceEntity block => new BlockReferenceDto
            {
                DefinitionId = block.DefinitionId,
                Position = ToDto(block.Position),
                Scale = block.Scale,
                Rotation = block.Rotation,
                Attributes = block.Attributes.Select(kv => new BlockAttributeDto { Key = kv.Key, Value = kv.Value }).ToList(),
            },
            ParametricSymbolEntity symbol => new ParametricSymbolDto
            {
                SymbolKey = symbol.SymbolKey,
                Position = ToDto(symbol.Position),
                Scale = symbol.Scale,
                Rotation = symbol.Rotation,
                Parameters = symbol.Parameters.Select(kv => new ParameterValueDto { Name = kv.Key, Value = kv.Value }).ToList(),
            },
            SectionMarkEntity section => new SectionMarkDto
            {
                Start = ToDto(section.Start),
                End = ToDto(section.End),
                Label = section.Label,
                Reference = section.Reference,
                Size = section.Size,
                FlipDirection = section.FlipDirection,
            },
            DetailMarkEntity detail => new DetailMarkDto
            {
                Center = ToDto(detail.Center),
                Radius = detail.Radius,
                Label = detail.Label,
                Reference = detail.Reference,
                TextHeight = detail.TextHeight,
            },
            WallEntity wall => new WallDto
            {
                Start = ToDto(wall.Start),
                End = ToDto(wall.End),
                Thickness = wall.Thickness,
                Height = wall.Height,
                BaseElevation = wall.BaseElevation,
            },
            _ => throw new NotSupportedException($"Entity type '{entity.GetType().Name}' cannot be serialized."),
        };

        dto.LayerId = entity.LayerId;
        dto.PartialDrawingId = entity.PartialDrawingId;
        dto.GroupId = entity.GroupId;
        dto.Stroke = entity.StrokeOverride is { } stroke ? ToDto(stroke) : null;
        dto.IsConstruction = entity.IsConstruction;
        return dto;
    }

    private static IEntity FromDto(EntityDto dto)
    {
        IEntity entity = dto switch
        {
            LineDto line => new LineEntity(FromDto(line.Start), FromDto(line.End)),
            CircleDto circle => new CircleEntity(FromDto(circle.Center), circle.Radius),
            ArcDto arc => new ArcEntity(FromDto(arc.Center), arc.Radius, arc.StartAngle, arc.SweepAngle),
            PolylineDto poly => new PolylineEntity(poly.Points.Select(FromDto), poly.Bulges, poly.Closed)
            {
                SolidFill = poly.Filled
                    ? new FillStyle(FromDto(poly.FillColor), FromDto(poly.FillColor2), poly.FillGradient, poly.FillAngle)
                    : null,
            },
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
            ImageDto image => new ImageEntity(
                Convert.FromBase64String(image.Data), FromDto(image.Origin), image.Width, image.Height, image.Rotation),
            LinearDimensionDto dim => FillDim(new LinearDimensionEntity(
                FromDto(dim.P1), FromDto(dim.P2), FromDto(dim.DimLinePoint),
                Enum.TryParse(dim.Orientation, out LinearDimensionKind kind) ? kind : LinearDimensionKind.Aligned), dim),
            AngularDimensionDto dim => FillDim(new AngularDimensionEntity(
                FromDto(dim.Vertex), FromDto(dim.P1), FromDto(dim.P2), FromDto(dim.ArcPoint)), dim),
            RadialDimensionDto dim => FillDim(new RadialDimensionEntity(
                FromDto(dim.Center), FromDto(dim.EdgePoint), FromDto(dim.TextPoint), dim.IsDiameter), dim),
            ElevationDimensionDto dim => FillDim(new ElevationDimensionEntity(
                FromDto(dim.Position), dim.ZValue), dim),
            OrdinateDimensionDto dim => FillDim(new OrdinateDimensionEntity(
                FromDto(dim.Position), FromDto(dim.LeaderEnd), FromDto(dim.Origin)), dim),
            BlockReferenceDto block => FromBlockReference(block),
            ParametricSymbolDto symbol => new ParametricSymbolEntity(
                symbol.SymbolKey, FromDto(symbol.Position),
                symbol.Parameters.ToDictionary(p => p.Name, p => p.Value), symbol.Scale, symbol.Rotation),
            SectionMarkDto section => new SectionMarkEntity(
                FromDto(section.Start), FromDto(section.End), section.Label, section.Reference,
                section.Size, section.FlipDirection),
            DetailMarkDto detail => new DetailMarkEntity(
                FromDto(detail.Center), detail.Radius, detail.Label, detail.Reference, detail.TextHeight),
            WallDto wall => new WallEntity(
                FromDto(wall.Start), FromDto(wall.End), wall.Thickness, wall.Height, wall.BaseElevation),
            _ => throw new NotSupportedException($"Entity DTO '{dto.GetType().Name}' cannot be deserialized."),
        };

        entity.LayerId = dto.LayerId;
        entity.PartialDrawingId = dto.PartialDrawingId;
        entity.GroupId = dto.GroupId;
        entity.StrokeOverride = dto.Stroke is { } stroke ? FromDto(stroke) : null;
        entity.IsConstruction = dto.IsConstruction;
        return entity;
    }

    private static BlockReferenceEntity FromBlockReference(BlockReferenceDto dto)
    {
        var reference = new BlockReferenceEntity(dto.DefinitionId, FromDto(dto.Position), dto.Scale, dto.Rotation);
        foreach (BlockAttributeDto attribute in dto.Attributes)
            reference.Attributes[attribute.Key] = attribute.Value;
        return reference;
    }

    private static TextHAlign ParseHAlign(string value)
        => Enum.TryParse(value, out TextHAlign align) ? align : TextHAlign.Left;

    private static TextVAlign ParseVAlign(string value)
        => Enum.TryParse(value, out TextVAlign align) ? align : TextVAlign.Baseline;
}
