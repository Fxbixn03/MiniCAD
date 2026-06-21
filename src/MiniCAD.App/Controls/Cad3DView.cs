using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Viewing;
using MiniCAD.Renderer.Rendering;
using CoreColor = MiniCAD.Core.Styling.Color;

namespace MiniCAD.App.Controls;

/// <summary>
/// Displays the document's 3D model space as a wireframe through a <see cref="Camera3D"/>.
/// Left-drag orbits, middle-drag pans, the wheel zooms. Drawing is delegated to the Renderer's
/// <see cref="Skia3DSceneRenderer"/> via a CPU <see cref="WriteableBitmap"/>, so the App stays
/// Skia-free (same approach as the 2D canvas).
/// </summary>
public sealed class Cad3DView : Control
{
    public static readonly StyledProperty<CadDocument?> DocumentProperty =
        AvaloniaProperty.Register<Cad3DView, CadDocument?>(nameof(Document));

    public static readonly StyledProperty<Camera3D?> CameraProperty =
        AvaloniaProperty.Register<Cad3DView, Camera3D?>(nameof(Camera));

    private static readonly CoreColor Background = new(12, 16, 30);

    private readonly Skia3DSceneRenderer _renderer = new();
    private WriteableBitmap? _bitmap;
    private bool _sceneInvalid = true;
    private bool _autoFitPending = true;
    private Point _lastPointer;
    private bool _orbiting;
    private bool _panning;

    public Cad3DView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public CadDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public Camera3D? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

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
            Invalidate();
        }
        else if (change.Property == CameraProperty)
        {
            if (change.OldValue is Camera3D oldCam)
                oldCam.Changed -= OnCameraChanged;
            if (change.NewValue is Camera3D newCam)
                newCam.Changed += OnCameraChanged;
            Invalidate();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateCameraSize();
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.ModelsChanged or DocumentChangeKind.Reloaded)
            _autoFitPending = true;
        Invalidate();
    }

    private void OnCameraChanged(object? sender, EventArgs e) => Invalidate();

    private void Invalidate()
    {
        _sceneInvalid = true;
        InvalidateVisual();
    }

    private void UpdateCameraSize()
    {
        if (Camera is not { } camera || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        double scaling = Scaling;
        camera.Resize(Math.Max(1, Bounds.Width * scaling), Math.Max(1, Bounds.Height * scaling));

        if (_autoFitPending && Document?.GetModelBounds() is { } bounds)
        {
            camera.ZoomToFit(bounds);
            _autoFitPending = false;
        }
    }

    public override void Render(Avalonia.Media.DrawingContext context)
    {
        base.Render(context);
        if (Document is not { } document || Camera is not { } camera)
            return;

        UpdateCameraSize();
        int w = (int)Math.Max(1, camera.Width);
        int h = (int)Math.Max(1, camera.Height);
        EnsureBitmap(w, h);

        if (_sceneInvalid)
        {
            using (ILockedFramebuffer fb = _bitmap!.Lock())
                _renderer.Render(document, camera, fb.Address, fb.Size.Width, fb.Size.Height, fb.RowBytes, Background);
            _sceneInvalid = false;
        }

        context.DrawImage(_bitmap!, new Rect(0, 0, w, h), new Rect(Bounds.Size));
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is { } b && b.PixelSize.Width == width && b.PixelSize.Height == height)
            return;
        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _sceneInvalid = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (Camera is { } camera)
        {
            camera.Zoom(e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1);
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        _lastPointer = e.GetPosition(this);
        _orbiting = props.IsLeftButtonPressed;
        _panning = props.IsMiddleButtonPressed;
        if (_orbiting || _panning)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Camera is not { } camera || (!_orbiting && !_panning))
            return;

        Point now = e.GetPosition(this);
        double dx = now.X - _lastPointer.X;
        double dy = now.Y - _lastPointer.Y;
        _lastPointer = now;

        if (_orbiting)
            camera.Orbit(dx * 0.01, -dy * 0.01);
        else
            camera.Pan(dx * Scaling, dy * Scaling);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _orbiting = false;
        _panning = false;
        e.Pointer.Capture(null);
    }
}
