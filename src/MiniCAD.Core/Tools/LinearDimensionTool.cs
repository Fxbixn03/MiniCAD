using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Places a linear/aligned dimension: click the first point, the second point, then a point the
/// dimension line runs through (its offset/side). The measurement is computed automatically and
/// the active <see cref="DimStyle"/> is applied. Object snapping helps hit exact endpoints.
/// </summary>
public sealed class LinearDimensionTool : ToolBase
{
    private Point2D? _p1;
    private Point2D? _p2;
    private Point2D _cursor;
    private bool _hasCursor;

    // Continued/baseline reference, valid after the first dimension of a run is placed.
    private bool _hasReference;
    private Point2D _baseStart;     // first point (baseline origin / chain start)
    private Point2D _lastEnd;       // previous dimension's second point
    private Point2D _refDimLine;    // the first dimension line point
    private int _baselineStep;

    /// <summary>Aligned (parallel), horizontal or vertical dimension line.</summary>
    public LinearDimensionKind Kind { get; set; } = LinearDimensionKind.Aligned;

    /// <summary>Whether following clicks chain or baseline-stack off the first dimension.</summary>
    public DimensionContinueMode ContinueMode { get; set; } = DimensionContinueMode.None;

    public override string Name => "Maß";

    protected override bool HasActiveOperation => _p1 is not null || _hasReference;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            if (HasActiveOperation)
                Cancel();
            else
                Context.ReturnToDefaultTool();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        Point2D world = Snap(input);

        // In a continued/baseline run a single click adds the next dimension off the reference.
        if (_hasReference && _p1 is null)
        {
            AddContinued(world);
            Context.RequestRedraw();
            return;
        }

        if (_p1 is null)
        {
            _p1 = world;
        }
        else if (_p2 is null)
        {
            _p2 = world;
        }
        else
        {
            Commit(_p1.Value, _p2.Value, world);
        }

        Context.RequestRedraw();
    }

    private void Commit(Point2D p1, Point2D p2, Point2D dimLinePoint)
    {
        var dim = new LinearDimensionEntity(p1, p2, dimLinePoint, Kind);
        CadDocument.ApplyDimStyle(dim, Context.Document.ActiveDimStyle);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(dim)));

        _p1 = null;
        _p2 = null;
        ClearSnap();

        if (ContinueMode == DimensionContinueMode.None)
        {
            _hasReference = false;
            return;
        }

        // Remember the run so the next click continues it.
        _hasReference = true;
        _baseStart = p1;
        _lastEnd = p2;
        _refDimLine = dimLinePoint;
        _baselineStep = 0;
    }

    private void AddContinued(Point2D world)
    {
        if (ContinueMode == DimensionContinueMode.Baseline)
            _baselineStep++;

        LinearDimensionEntity dim = ContinuedDim(world, _baselineStep);
        Context.Execute(new AddEntityCommand(Context.Document, ApplyDefaultStyle(dim)));

        _lastEnd = world;
        ClearSnap();
    }

    /// <summary>Builds (without committing) the next continued/baseline dimension to a point.</summary>
    private LinearDimensionEntity ContinuedDim(Point2D world, int baselineStep)
    {
        Point2D p1, dimLinePoint;
        if (ContinueMode == DimensionContinueMode.Baseline)
        {
            p1 = _baseStart;
            Vector2D dir = DirectionFor(_baseStart, world);
            Vector2D normal = dir.Perpendicular();
            if ((_refDimLine - _baseStart).Dot(normal) < 0)
                normal = -normal;
            double step = Math.Max(Context.Document.ActiveDimStyle.TextHeight, 1.0) * 2.5;
            dimLinePoint = _refDimLine + normal * (step * baselineStep);
        }
        else // Chain
        {
            p1 = _lastEnd;
            dimLinePoint = _refDimLine;
        }

        var dim = new LinearDimensionEntity(p1, world, dimLinePoint, Kind);
        CadDocument.ApplyDimStyle(dim, Context.Document.ActiveDimStyle);
        return dim;
    }

    private Vector2D DirectionFor(Point2D a, Point2D b) => Kind switch
    {
        LinearDimensionKind.Horizontal => Vector2D.UnitX,
        LinearDimensionKind.Vertical => Vector2D.UnitY,
        _ => (b - a).Normalized() is { } d && d != Vector2D.Zero ? d : Vector2D.UnitX,
    };

    public override void PointerMove(in ToolPointerInput input)
    {
        _cursor = Snap(input);
        _hasCursor = true;
        Context.RequestRedraw();
    }

    public override void Cancel() => Reset();

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(2);

        if (_p1 is { } p1 && _p2 is null && _hasCursor)
        {
            items.Add(new OverlayItem(new LineEntity(p1, _cursor), ToolStyle.Preview));
        }
        else if (_p1 is { } a && _p2 is { } b && _hasCursor)
        {
            var preview = new LinearDimensionEntity(a, b, _cursor, Kind);
            CadDocument.ApplyDimStyle(preview, Context.Document.ActiveDimStyle);
            items.Add(new OverlayItem(preview, ToolStyle.Preview));
        }
        else if (_hasReference && _p1 is null && _hasCursor)
        {
            int previewStep = ContinueMode == DimensionContinueMode.Baseline ? _baselineStep + 1 : _baselineStep;
            items.Add(new OverlayItem(ContinuedDim(_cursor, previewStep), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private void Reset()
    {
        _p1 = null;
        _p2 = null;
        _hasReference = false;
        _baselineStep = 0;
        ClearSnap();
    }
}
