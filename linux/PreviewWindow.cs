using System;
using System.Linq;
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
    private readonly CaptureJob _capture;
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

    public PreviewWindow(WindowItem item, CaptureJob capture)
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
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) StartDrag(DragMode.Move, e);
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
            StartDrag(DragMode.Resize, e);
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
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) StartDrag(DragMode.Move, e);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        PointerMoved += (_, e) => ContinueDrag(e);
        PointerReleased += (_, e) => EndDrag(e);
        PointerCaptureLost += (_, __) => _drag = DragMode.None;

        PositionChanged += (_, __) => Restart(_settle);
        SizeChanged += (_, __) => Restart(_settle);
        _settle.Tick += (_, __) =>
        {
            _settle.Stop();
            if (_drag != DragMode.None) return; // still dragging: settle again after release
            Snap();
            EnsureOnScreen();
            LayoutSettled?.Invoke(this, EventArgs.Empty);
        };
        Closed += (_, __) =>
        {
            _settle.Stop();
        };
    }

    public event EventHandler? Clicked;

    /// <summary>Moved or resized and stopped there; time to save the layout.</summary>
    public event EventHandler? LayoutSettled;

    public IntPtr SourceHwnd => _item.HWnd;
    public string WindowTitle => _item.Title;
    public int OccurrenceIndex { get; set; }

    /// <summary>Where the preview should open; re-applied once it is mapped.</summary>
    public PixelPoint? WantedPosition { get; set; }

    /// <summary>The preview's own X window, for opacity and "is this one of ours".</summary>
    public IntPtr OwnHandle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    private static void Restart(DispatcherTimer t)
    {
        t.Stop();
        t.Start();
    }

    // ===== Drag (move / resize) =====

    // Done here instead of BeginMoveDrag/BeginResizeDrag: those hand the drag to the window
    // manager (_NET_WM_MOVERESIZE), which Cinnamon ignored for these borderless, never-activated
    // previews — the user could not move them at all.
    private enum DragMode { None, Move, Resize }

    private DragMode _drag;
    private PixelPoint _dragStartPointer;
    private PixelPoint _dragStartPosition;
    private Size _dragStartSize;

    private void StartDrag(DragMode mode, PointerPressedEventArgs e)
    {
        _drag = mode;
        _dragStartPointer = this.PointToScreen(e.GetPosition(this));
        _dragStartPosition = Position;
        _dragStartSize = new Size(Bounds.Width, Bounds.Height);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void ContinueDrag(PointerEventArgs e)
    {
        if (_drag == DragMode.None) return;
        var now = this.PointToScreen(e.GetPosition(this));
        int dx = now.X - _dragStartPointer.X, dy = now.Y - _dragStartPointer.Y;
        if (_drag == DragMode.Move)
        {
            Position = new PixelPoint(_dragStartPosition.X + dx, _dragStartPosition.Y + dy);
        }
        else
        {
            Width = Math.Max(MinWidth, _dragStartSize.Width + dx / RenderScaling);
            Height = Math.Max(MinHeight, _dragStartSize.Height + dy / RenderScaling);
        }
    }

    private void EndDrag(PointerReleasedEventArgs e)
    {
        if (_drag == DragMode.None) return;
        _drag = DragMode.None;
        e.Pointer.Capture(null);
        Restart(_settle);
    }

    /// <summary>
    /// Pulls the preview back when its title bar is outside every monitor. With monitors of
    /// different sizes the desktop has dead corners (e.g. above a shorter screen) where a window
    /// can sit unreachable.
    /// </summary>
    public void EnsureOnScreen()
    {
        var all = Screens.All;
        if (all.Count == 0) return;

        var p = Position;
        int w = (int)Math.Round(Bounds.Width > 0 ? Bounds.Width : Width);
        // A grab point on the title bar: what the user needs to reach.
        var grab = new PixelPoint(p.X + Math.Min(40, w / 2), p.Y + TitleBarHeight / 2);
        foreach (var screen in all)
        {
            if (screen.Bounds.Contains(grab)) return;
        }

        // No monitor holds the grab point: move into the nearest one, keeping the window whole.
        var area = all.OrderBy(sc => Distance(sc.Bounds, grab)).First().WorkingArea;
        int h = (int)Math.Round(Bounds.Height > 0 ? Bounds.Height : Height);
        int x = Math.Clamp(p.X, area.X, Math.Max(area.X, area.Right - w));
        int y = Math.Clamp(p.Y, area.Y, Math.Max(area.Y, area.Bottom - h));
        Position = new PixelPoint(x, y);
    }

    private static double Distance(PixelRect r, PixelPoint p)
    {
        int dx = Math.Max(Math.Max(r.X - p.X, 0), p.X - r.Right);
        int dy = Math.Max(Math.Max(r.Y - p.Y, 0), p.Y - r.Bottom);
        return Math.Sqrt((double)dx * dx + (double)dy * dy);
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
        var src = _capture.SourceSize;
        if (!ThumbnailGeometry.NeedsCrop(_region) || src.cx <= 0 || src.cy <= 0) return;
        double cw = Math.Max(1.0, src.cx * _region!.W);
        double ch = Math.Max(1.0, src.cy * _region.H);
        Height = Math.Max(MinHeight, Width * (ch / cw) + TitleBarHeight);
    }

    // ===== Frames =====

    /// <summary>
    /// Called by the UI frame timer: tells the capture thread what this preview needs now and
    /// shows the newest frame it finished. No X round trip happens here.
    /// </summary>
    public void RenderFrame()
    {
        var scale = RenderScaling;
        int areaW = (int)Math.Round(_imageHost.Bounds.Width * scale);
        int areaH = (int)Math.Round(_imageHost.Bounds.Height * scale);
        if (areaW < 2 || areaH < 2) return;

        bool zoomed = _zoom.InternalZoom && IsPointerOver;
        _capture.Request = new CaptureRequest(areaW, areaH, _region?.Clone(), zoomed, _zoom);

        _capture.TryTake((pixels, w, h) =>
        {
            if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96 * scale, 96 * scale),
                    AvPixelFormat.Bgra8888, AlphaFormat.Opaque);
                _image.Source = _bitmap;
            }
            using (var fb = _bitmap.Lock())
            {
                if (fb.RowBytes == w * 4)
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, w * h * 4);
                else
                    for (int y = 0; y < h; y++)
                        System.Runtime.InteropServices.Marshal.Copy(pixels, y * w * 4, fb.Address + y * fb.RowBytes, w * 4);
            }
            _image.InvalidateVisual();
        });
    }
}
