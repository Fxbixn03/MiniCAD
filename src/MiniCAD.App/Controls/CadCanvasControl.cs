using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;
using MiniCAD.Core.Viewing;
using MiniCAD.Renderer.Rendering;
using CoreColor = MiniCAD.Core.Styling.Color;

namespace MiniCAD.App.Controls;

/// <summary>
/// Avalonia control that displays a <see cref="CadDocument"/> through a <see cref="Viewport"/>
/// and drives the active tool. Drawing is delegated to the Renderer layer via a CPU
/// <see cref="WriteableBitmap"/>, so the App project never references SkiaSharp directly.
///
/// Input mapping: middle-button drag pans, the wheel zooms, and left/right button plus the
/// keyboard are translated into world coordinates and forwarded to the <see cref="Tools"/>.
/// </summary>
public sealed class CadCanvasControl : Control
{
    public static readonly StyledProperty<CadDocument?> DocumentProperty =
        AvaloniaProperty.Register<CadCanvasControl, CadDocument?>(nameof(Document));

    public static readonly StyledProperty<Viewport?> ViewportProperty =
        AvaloniaProperty.Register<CadCanvasControl, Viewport?>(nameof(Viewport));

    public static readonly StyledProperty<ToolManager?> ToolsProperty =
        AvaloniaProperty.Register<CadCanvasControl, ToolManager?>(nameof(Tools));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<CadCanvasControl, bool>(nameof(ShowGrid), defaultValue: true);

    public static readonly StyledProperty<Color> SceneBackgroundProperty =
        AvaloniaProperty.Register<CadCanvasControl, Color>(nameof(SceneBackground), Color.FromRgb(30, 30, 34));

    // Full-width/height CAD crosshair that replaces the OS cursor inside the canvas.
    private static readonly IPen CrosshairPen =
        new Pen(new SolidColorBrush(Color.FromArgb(170, 220, 220, 225)), 1);

    private readonly SkiaSceneRenderer _renderer = new();
    private WriteableBitmap? _bitmap;
    private bool _autoFitPending = true;
    private bool _sceneInvalid = true;
    private bool _isPanning;
    private Point _lastPointer;
    private Point _cursorPosition;
    private bool _cursorInside;

    public CadCanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
        // Hide the OS cursor; the crosshair drawn in Render takes its place.
        Cursor = new Cursor(StandardCursorType.None);
    }

    public CadDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public Viewport? Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public ToolManager? Tools
    {
        get => GetValue(ToolsProperty);
        set => SetValue(ToolsProperty, value);
    }

    /// <summary>Whether the reference grid is drawn.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>The canvas background (clear) color.</summary>
    public Color SceneBackground
    {
        get => GetValue(SceneBackgroundProperty);
        set => SetValue(SceneBackgroundProperty, value);
    }

    /// <summary>Raised as the cursor moves, with the world-space position under it.</summary>
    public event EventHandler<Point2D>? CursorWorldMoved;

    private double Scaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DocumentProperty)
        {
            if (change.OldValue is CadDocument oldDoc)
                oldDoc.Changed -= OnDocumentChanged;
            if (change.NewValue is CadDocument newDoc)
                newDoc.Changed += OnDocumentChanged;
            _autoFitPending = true;
            UpdateViewportMetrics();
            InvalidateScene();
        }
        else if (change.Property == ViewportProperty)
        {
            if (change.OldValue is Viewport oldVp)
                oldVp.Changed -= OnViewportChanged;
            if (change.NewValue is Viewport newVp)
                newVp.Changed += OnViewportChanged;
            UpdateViewportMetrics();
            InvalidateScene();
        }
        else if (change.Property == ToolsProperty)
        {
            if (change.OldValue is ToolManager oldTools)
            {
                oldTools.RedrawRequested -= OnInvalidationRequested;
                oldTools.ActiveToolChanged -= OnInvalidationRequested;
            }
            if (change.NewValue is ToolManager newTools)
            {
                newTools.RedrawRequested += OnInvalidationRequested;
                newTools.ActiveToolChanged += OnInvalidationRequested;
            }
            InvalidateScene();
        }
        else if (change.Property == ShowGridProperty || change.Property == SceneBackgroundProperty)
        {
            InvalidateScene();
        }
        else if (change.Property == BoundsProperty)
        {
            // Runs during the layout pass (not the render pass), so it is safe to resize
            // the viewport and trigger the auto-fit here.
            UpdateViewportMetrics();
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        // A loaded project replaces the whole document — re-fit the view to its content.
        if (e.Kind == DocumentChangeKind.Reloaded)
        {
            _autoFitPending = true;
            UpdateViewportMetrics();
        }

        InvalidateScene();
    }

    private void OnViewportChanged(object? sender, EventArgs e) => InvalidateScene();

    private void OnInvalidationRequested(object? sender, EventArgs e) => InvalidateScene();

    /// <summary>Marks the rendered scene dirty so it is re-rasterized on the next paint.</summary>
    private void InvalidateScene()
    {
        _sceneInvalid = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Syncs the viewport's device-pixel size to the control and performs the one-time
    /// zoom-to-fit. Must never be called from <see cref="Render"/>: mutating the viewport
    /// raises <see cref="Viewport.Changed"/>, and invalidating during the render pass throws.
    /// </summary>
    private void UpdateViewportMetrics()
    {
        if (Document is not { } document || Viewport is not { } viewport)
            return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        double scaling = Scaling;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scaling));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scaling));
        viewport.Resize(pixelWidth, pixelHeight);

        if (_autoFitPending)
        {
            // Fit once to whatever is there on first layout; an empty document just keeps
            // the default view centered on the origin. Clearing the flag regardless avoids
            // a surprise zoom later when the user draws the first entity and then resizes.
            if (document.GetContentBounds() is { } contentBounds)
                viewport.ZoomToFit(contentBounds);
            _autoFitPending = false;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Document is not { } document || Viewport is not { } viewport)
            return;

        // Size is established in UpdateViewportMetrics during layout; render is read-only.
        int pixelWidth = (int)viewport.Width;
        int pixelHeight = (int)viewport.Height;
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return;

        EnsureBitmap(pixelWidth, pixelHeight);

        // Re-rasterize the scene only when something other than the cursor changed; a moving
        // crosshair then just re-blits the cached bitmap and redraws two lines.
        if (_sceneInvalid)
        {
            _renderer.ShowGrid = ShowGrid;
            Color bg = SceneBackground;
            var background = new CoreColor(bg.R, bg.G, bg.B, bg.A);

            using (ILockedFramebuffer framebuffer = _bitmap!.Lock())
            {
                _renderer.Render(
                    document,
                    viewport,
                    framebuffer.Address,
                    framebuffer.Size.Width,
                    framebuffer.Size.Height,
                    framebuffer.RowBytes,
                    background,
                    Tools?.GetOverlay());
            }

            _sceneInvalid = false;
        }

        context.DrawImage(
            _bitmap!,
            new Rect(0, 0, pixelWidth, pixelHeight),
            new Rect(Bounds.Size));

        DrawCrosshair(context);
    }

    private void DrawCrosshair(DrawingContext context)
    {
        if (!_cursorInside)
            return;

        double x = _cursorPosition.X;
        double y = _cursorPosition.Y;
        context.DrawLine(CrosshairPen, new Point(0, y), new Point(Bounds.Width, y));
        context.DrawLine(CrosshairPen, new Point(x, 0), new Point(x, Bounds.Height));
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is { } bitmap && bitmap.PixelSize.Width == width && bitmap.PixelSize.Height == height)
            return;

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        _sceneInvalid = true; // fresh (empty) bitmap must be re-rendered
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (Viewport is not { } viewport)
            return;

        Point2D anchor = ToDevicePoint(e.GetPosition(this));
        double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        viewport.ZoomAt(anchor, factor);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        PointerPoint point = e.GetCurrentPoint(this);
        PointerPointProperties props = point.Properties;

        if (props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPointer = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (Tools is not { } tools || Viewport is null)
            return;

        ToolButton button = props.IsRightButtonPressed ? ToolButton.Right
            : props.IsLeftButtonPressed ? ToolButton.Left
            : ToolButton.None;
        if (button == ToolButton.None)
            return;

        tools.PointerDown(BuildInput(point.Position, button, e.KeyModifiers, e.ClickCount));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        // Track the cursor so the crosshair follows it (repaint even when nothing else changed).
        _cursorPosition = e.GetPosition(this);
        _cursorInside = true;
        InvalidateVisual();

        if (_isPanning && Viewport is { } viewport)
        {
            double scaling = Scaling;
            viewport.PanByScreenDelta(
                (_cursorPosition.X - _lastPointer.X) * scaling,
                (_cursorPosition.Y - _lastPointer.Y) * scaling);
            _lastPointer = _cursorPosition;
            return;
        }

        if (Viewport is { } vp)
            CursorWorldMoved?.Invoke(this, vp.ScreenToWorld(ToDevicePoint(_cursorPosition)));

        if (Tools is { } tools && Viewport is not null)
            tools.PointerMove(BuildInput(_cursorPosition, ToolButton.None, e.KeyModifiers, 0));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _cursorPosition = e.GetPosition(this);
        _cursorInside = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _cursorInside = false;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            return;
        }

        if (Tools is { } tools && Viewport is not null)
        {
            ToolButton button = e.InitialPressMouseButton switch
            {
                MouseButton.Left => ToolButton.Left,
                MouseButton.Right => ToolButton.Right,
                MouseButton.Middle => ToolButton.Middle,
                _ => ToolButton.None,
            };
            tools.PointerUp(BuildInput(e.GetPosition(this), button, e.KeyModifiers, 0));
        }

        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Tools is not { } tools)
            return;

        ToolKey key = e.Key switch
        {
            Key.Escape => ToolKey.Escape,
            Key.Enter => ToolKey.Enter,
            Key.Delete => ToolKey.Delete,
            Key.Back => ToolKey.Delete,
            _ => ToolKey.None,
        };
        if (key == ToolKey.None)
            return;

        tools.KeyDown(key);
        e.Handled = true;
    }

    private ToolPointerInput BuildInput(Point position, ToolButton button, KeyModifiers modifiers, int clickCount)
        => new()
        {
            World = Viewport!.ScreenToWorld(ToDevicePoint(position)),
            Button = button,
            Modifiers = MapModifiers(modifiers),
            ClickCount = clickCount,
        };

    private static ToolModifiers MapModifiers(KeyModifiers modifiers)
    {
        ToolModifiers result = ToolModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            result |= ToolModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Control))
            result |= ToolModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            result |= ToolModifiers.Alt;
        return result;
    }

    private Point2D ToDevicePoint(Point dipPoint)
    {
        double scaling = Scaling;
        return new Point2D(dipPoint.X * scaling, dipPoint.Y * scaling);
    }
}
