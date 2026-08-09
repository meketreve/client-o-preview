using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview;

public partial class StreamWindow : Window
{
    private readonly MainWindow _owner;
    private readonly WindowItem _item;
    private IntPtr _thumb = IntPtr.Zero;
    private Zoom _zoomSettings = new();
    private bool _isZoomed = false;
    private double _originalWidth = 0;
    private double _originalHeight = 0;
    private bool _snapToGrid = false;
    private int _gridSize = 20;
    private RegionPreset? _region;

    public string WindowTitle => _item.Title;
    public IntPtr SourceHwnd => _item.HWnd;
    public int OccurrenceIndex { get; set; } = 0;

    public StreamWindow(MainWindow owner, WindowItem item)
    {
        _owner = owner;
        _item = item;
        InitializeComponent();
        Title = $"Stream: {_item.Title}";
        TxtTitle.Text = $"{_item.Title}  (0x{_item.HWnd.ToInt64():X})";
    }

    private static readonly System.Windows.Media.Brush IdleBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));

    private System.Windows.Media.Brush _activeColorBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 100, 200));

    // The foreground timer calls SetActiveState 2.5x a second per preview; without this
    // the title bar allocated a fresh brush on every tick.
    private bool? _isActive;

    public void SetOpacity(double alpha) => Opacity = alpha;

    public void SetTitleFontSize(int fontSize)
    {
        TxtTitle.FontSize = fontSize;
    }

    public void SetHighlightColor(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            _activeColorBrush = new System.Windows.Media.SolidColorBrush(color);
            if (_isActive == true) TitleBar.Background = _activeColorBrush;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"invalid highlight colour '{hex}'", ex);
        }
    }

    public void SetActiveState(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        TitleBar.Background = active ? _activeColorBrush : IdleBrush;
    }
    public void SetSize(int w, int h)
    {
        Width = Math.Max(120, w + 16);
        Height = Math.Max(90, h + 48);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HideFromAltTab();
        EnsureThumbnail();
        UpdateThumbnailRect();
    }

    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        // Remove APPWINDOW flag and add TOOLWINDOW flag
        exStyle &= ~WS_EX_APPWINDOW;
        exStyle |= WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void EnsureThumbnail()
    {
        var dest = new WindowInteropHelper(this).Handle;
        if (_thumb != IntPtr.Zero)
        {
            DwmUnregisterThumbnail(_thumb);
            _thumb = IntPtr.Zero;
        }
        var hr = DwmRegisterThumbnail(dest, _item.HWnd, out _thumb);
        if (hr != 0)
        {
            // The window stays up without a preview; the source probably died mid-open.
            _thumb = IntPtr.Zero;
            AppLog.Warn($"DwmRegisterThumbnail failed for '{_item.Title}'", $"hr=0x{hr:X8}");
        }
    }

    private void UpdateThumbnailRect(bool zoomed = false)
    {
        if (_thumb == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));

        int titleOffset = (int)Math.Round(TitleBar.ActualHeight * dpi.DpiScaleY);
        int h = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY) - titleOffset);

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY,
            rcDestination = new RECT { Left = 0, Top = titleOffset, Right = w, Bottom = titleOffset + h },
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false
        };

        bool internalZoom = zoomed && _zoomSettings.InternalZoom;
        bool cropped = ThumbnailGeometry.NeedsCrop(_region);

        if ((cropped || internalZoom) && DwmQueryThumbnailSourceSize(_thumb, out SIZE srcSize) == 0
            && srcSize.cx > 0 && srcSize.cy > 0)
        {
            var source = ThumbnailGeometry.CropSource(srcSize, _region, internalZoom, _zoomSettings);

            props.dwFlags |= DWM_TNP_RECTSOURCE;
            props.rcSource = source;

            if (cropped && _region!.LockAspect)
                props.rcDestination = ThumbnailGeometry.Letterbox(
                    props.rcDestination, source.Right - source.Left, source.Bottom - source.Top);
        }

        DwmUpdateThumbnailProperties(_thumb, ref props);
    }

    public void ApplyRegion(RegionPreset? region)
    {
        _region = region;
        UpdateRegionBadge();
        UpdateThumbnailRect(_isZoomed);
    }

    private void UpdateRegionBadge()
    {
        bool active = ThumbnailGeometry.NeedsCrop(_region);
        TxtRegion.Text = active ? $"▣ {_region!.Name}" : string.Empty;
        TxtRegion.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    // Reshapes the preview so the cropped area fills it without black bars.
    public void FitToRegion()
    {
        if (_region == null || !ThumbnailGeometry.NeedsCrop(_region)) return;
        if (_thumb == IntPtr.Zero) return;
        if (DwmQueryThumbnailSourceSize(_thumb, out SIZE srcSize) != 0) return;
        if (srcSize.cx <= 0 || srcSize.cy <= 0) return;

        double cw = Math.Max(1.0, srcSize.cx * _region.W);
        double ch = Math.Max(1.0, srcSize.cy * _region.H);

        double titleH = TitleBar.ActualHeight > 0 ? TitleBar.ActualHeight : 24;
        double contentW = Math.Max(60, ActualWidth > 0 ? ActualWidth : Width);
        Height = Math.Max(90, contentW * (ch / cw) + titleH);
    }

    private void BtnRegion_Click(object sender, RoutedEventArgs e)
    {
        _owner.OpenRegionPickerFor(this);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbnailRect(_isZoomed);
        _owner.SaveLayoutFor(this);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        if (_snapToGrid && _gridSize > 1)
        {
            double newLeft = Math.Round(Left / _gridSize) * _gridSize;
            double newTop = Math.Round(Top / _gridSize) * _gridSize;

            if (Math.Abs(newLeft - Left) > 0.1 || Math.Abs(newTop - Top) > 0.1)
            {
                Left = newLeft;
                Top = newTop;
                return; // Re-entry will happen
            }
        }

        base.OnLocationChanged(e);
        _owner.SaveLayoutFor(this);
    }

    public void ApplyGridSettings(bool enabled, int size)
    {
        _snapToGrid = enabled;
        _gridSize = size;
        if (_snapToGrid)
        {
            // Optional: snap immediately
            double newLeft = Math.Round(Left / _gridSize) * _gridSize;
            double newTop = Math.Round(Top / _gridSize) * _gridSize;
            Left = newLeft;
            Top = newTop;
        }
    }

    private void Content_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _owner.OnPreviewClicked(_item.HWnd);
    }

    private void Window_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        catch (InvalidOperationException)
        {
            // Ignora erro quando DragMove não pode ser chamado
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Ignora erro quando DragMove não pode ser chamado
        }
    }

    public void ApplyZoomSettings(Zoom zoom)
    {
        _zoomSettings = zoom;
        UpdateZoomState(IsMouseOver);
    }

    private void UpdateZoomState(bool mouseOver)
    {
        // Renamed to ResizeOnHover as requested
        bool shouldZoom = (_zoomSettings.ResizeOnHover || _zoomSettings.InternalZoom) && mouseOver;
        
        if (shouldZoom && !_isZoomed)
        {
            if (_zoomSettings.ResizeOnHover)
            {
                _originalWidth = Width;
                _originalHeight = Height;
                Width *= _zoomSettings.Magnification;
                Height *= _zoomSettings.Magnification;
            }
            _isZoomed = true;
            UpdateThumbnailRect(true);
        }
        else if (!shouldZoom && _isZoomed)
        {
            if (_zoomSettings.ResizeOnHover && _originalWidth > 0)
            {
                Width = _originalWidth;
                Height = _originalHeight;
            }
            _isZoomed = false;
            UpdateThumbnailRect(false);
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateZoomState(true);
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateZoomState(false);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_thumb != IntPtr.Zero)
        {
            DwmUnregisterThumbnail(_thumb);
            _thumb = IntPtr.Zero;
        }
    }
}
