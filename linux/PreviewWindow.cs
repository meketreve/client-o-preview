using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ClientOPreview.Linux.Platform;
using ClientOPreview.Models;
using ClientOPreview.Services;
using AvPixelFormat = Avalonia.Platform.PixelFormat;

namespace ClientOPreview.Linux;

/// <summary>
/// One live preview — the Linux StreamWindow. Borderless, title bar on top, frames pushed in
/// by <see cref="PreviewManager"/>'s capture timer.
///
/// Left click on the picture focuses the client; drag the title bar (or hold the right button
/// anywhere) to move; the corner grip resizes.
/// </summary>
internal sealed class PreviewWindow : Window
{
    private const int TitleBarHeight = 22;

    private readonly WindowItem _item;
    private readonly WindowCapture _capture;
    private readonly Border _titleBar;
    private readonly TextBlock _title;
    private readonly TextBlock _regionBadge;
    private readonly Image _image;
    private readonly Border _imageHost;

    private WriteableBitmap? _bitmap;
    private RegionPreset? _region;
    private Zoom _zoom = new();
    private IBrush _activeBrush = new SolidColorBrush(Color.FromRgb(40, 100, 200));
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));
    private bool? _isActive;

    // Moves and resizes arrive dozens of times per second during a drag; settings.json is
    // written once the window settles.
    private readonly DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _snapToGrid;
    private int _gridSize = 20;

    public PreviewWindow(WindowItem item, WindowCapture capture)
    {
        _item = item;
        _capture = capture;

        Title = $"Preview: {item.Title}";
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        CanResize = true;
        Background = Brushes.Black;
        MinWidth = 80;
        MinHeight = 50;
        ShowActivated = false;

        _title = new TextBlock
        {
            Text = item.Title,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(6, 0)
        };
        _regionBadge = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0),
            IsVisible = false
        };
        var titleRow = new DockPanel();
        DockPanel.SetDock(_regionBadge, Dock.Right);
        titleRow.Children.Add(_regionBadge);
        titleRow.Children.Add(_title);

        _titleBar = new Border { Height = TitleBarHeight, Background = IdleBrush, Child = titleRow, Cursor = new Cursor(StandardCursorType.SizeAll) };
        _titleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };

        _image = new Image { Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _imageHost = new Border { Background = Brushes.Black, Child = _image, ClipToBounds = true };
        _imageHost.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Clicked?.Invoke(this, EventArgs.Empty);
        };

        var grip = new Border
        {
            Width = 12, Height = 12,
            Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner)
        };
        grip.PointerPressed += (_, e) =>
        {
            BeginResizeDrag(WindowEdge.SouthEast, e);
            e.Handled = true;
        };

        var body = new Grid();
        body.Children.Add(_imageHost);
        body.Children.Add(grip);

        var root = new DockPanel();
        DockPanel.SetDock(_titleBar, Dock.Top);
        root.Children.Add(_titleBar);
        root.Children.Add(body);
        Content = root;

        // Right button held anywhere moves the preview, as on Windows.
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) BeginMoveDrag(e);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        PositionChanged += (_, __) => Restart(_settle);
        SizeChanged += (_, __) => Restart(_settle);
        _settle.Tick += (_, __) =>
        {
            _settle.Stop();
            Snap();
            LayoutSettled?.Invoke(this, EventArgs.Empty);
        };
        Closed += (_, __) =>
        {
            _settle.Stop();
            _capture.Dispose();
        };
    }

    public event EventHandler? Clicked;

    /// <summary>Moved or resized and stopped there; time to save the layout.</summary>
    public event EventHandler? LayoutSettled;

    public IntPtr SourceHwnd => _item.HWnd;
    public string WindowTitle => _item.Title;
    public int OccurrenceIndex { get; set; }

    /// <summary>The preview's own X window, for opacity and "is this one of ours".</summary>
    public IntPtr OwnHandle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    private static void Restart(DispatcherTimer t)
    {
        t.Stop();
        t.Start();
    }

    // ===== Settings =====

    public void SetSize(int w, int h)
    {
        Width = Math.Max(MinWidth, w);
        Height = Math.Max(MinHeight, h + TitleBarHeight);
    }

    public void SetTitleFontSize(int size) => _title.FontSize = _regionBadge.FontSize = Math.Clamp(size, 8, 20);

    public void SetHighlightColor(string hex)
    {
        if (Color.TryParse(hex, out var c))
        {
            _activeBrush = new SolidColorBrush(c);
            if (_isActive == true) _titleBar.Background = _activeBrush;
        }
        else
        {
            AppLog.Warn("Preview", $"invalid highlight colour '{hex}'");
        }
    }

    public void SetActiveState(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        _titleBar.Background = active ? _activeBrush : IdleBrush;
    }

    public void ApplyZoomSettings(Zoom zoom) => _zoom = zoom;

    public void ApplyGridSettings(bool enabled, int size)
    {
        _snapToGrid = enabled;
        _gridSize = size;
        Snap();
    }

    private void Snap()
    {
        if (!_snapToGrid || _gridSize <= 1) return;
        var p = Position;
        var snapped = new PixelPoint(
            (int)Math.Round(p.X / (double)_gridSize) * _gridSize,
            (int)Math.Round(p.Y / (double)_gridSize) * _gridSize);
        if (snapped != p) Position = snapped;
    }

    public void ApplyRegion(RegionPreset? region)
    {
        _region = region;
        bool active = ThumbnailGeometry.NeedsCrop(region);
        _regionBadge.Text = active ? $"▣ {region!.Name}" : string.Empty;
        _regionBadge.IsVisible = active;
    }

    /// <summary>Reshapes the preview so the crop fills it without black bars.</summary>
    public void FitToRegion()
    {
        if (!ThumbnailGeometry.NeedsCrop(_region) || !_capture.TryGetSourceSize(out var src)) return;
        double cw = Math.Max(1.0, src.cx * _region!.W);
        double ch = Math.Max(1.0, src.cy * _region.H);
        Height = Math.Max(MinHeight, Width * (ch / cw) + TitleBarHeight);
    }

    // ===== Frames =====

    /// <summary>Called by the capture timer. Keeps the last frame when there is nothing new.</summary>
    public void RenderFrame()
    {
        var scale = RenderScaling;
        int areaW = (int)Math.Round(_imageHost.Bounds.Width * scale);
        int areaH = (int)Math.Round(_imageHost.Bounds.Height * scale);
        if (areaW < 2 || areaH < 2 || !_capture.TryGetSourceSize(out var src)) return;

        bool zoomed = _zoom.InternalZoom && IsPointerOver;
        var crop = ThumbnailGeometry.CropSource(src, _region, zoomed, _zoom);
        var dest = new ClientOPreview.Native.NativeMethods.RECT { Left = 0, Top = 0, Right = areaW, Bottom = areaH };
        if (ThumbnailGeometry.NeedsCrop(_region) && _region!.LockAspect)
            dest = ThumbnailGeometry.Letterbox(dest, crop.Right - crop.Left, crop.Bottom - crop.Top);

        int w = dest.Right - dest.Left, h = dest.Bottom - dest.Top;
        if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96 * scale, 96 * scale),
                AvPixelFormat.Bgra8888, AlphaFormat.Opaque);
            _image.Source = _bitmap;
        }

        bool ok;
        using (var fb = _bitmap.Lock())
        {
            ok = fb.RowBytes == w * 4 && _capture.Capture(crop, w, h, fb.Address);
        }
        if (ok) _image.InvalidateVisual();
    }
}
