using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClientOPreview.Models;
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

    private System.Windows.Media.Brush _activeColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 100, 200));

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
        }
        catch { }
    }

    public void SetActiveState(bool active)
    {
        TitleBar.Background = active 
            ? _activeColorBrush
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));  // Default dark
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
            // falha silenciosa, mantém janela sem preview
            _thumb = IntPtr.Zero;
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
        bool cropped = _region != null && !IsFullWindow(_region);

        if ((cropped || internalZoom) && DwmQueryThumbnailSourceSize(_thumb, out SIZE srcSize) == 0
            && srcSize.cx > 0 && srcSize.cy > 0)
        {
            // Region first (it defines "what part of the client we watch"),
            // then the hover zoom magnifies inside that region.
            double left = srcSize.cx * (_region?.X ?? 0.0);
            double top = srcSize.cy * (_region?.Y ?? 0.0);
            double cw = Math.Max(1.0, srcSize.cx * (_region?.W ?? 1.0));
            double ch = Math.Max(1.0, srcSize.cy * (_region?.H ?? 1.0));

            if (internalZoom)
            {
                double mag = Math.Max(1.0, _zoomSettings.Magnification);
                double zw = cw / mag;
                double zh = ch / mag;
                left += (cw - zw) * _zoomSettings.OffsetX;
                top += (ch - zh) * _zoomSettings.OffsetY;
                cw = zw; ch = zh;
            }

            int sl = Clamp((int)Math.Round(left), 0, srcSize.cx - 1);
            int st = Clamp((int)Math.Round(top), 0, srcSize.cy - 1);
            int sr = Clamp(sl + (int)Math.Round(cw), sl + 1, srcSize.cx);
            int sb = Clamp(st + (int)Math.Round(ch), st + 1, srcSize.cy);

            props.dwFlags |= DWM_TNP_RECTSOURCE;
            props.rcSource = new RECT { Left = sl, Top = st, Right = sr, Bottom = sb };

            if (cropped && _region!.LockAspect)
                props.rcDestination = Letterbox(props.rcDestination, sr - sl, sb - st);
        }

        DwmUpdateThumbnailProperties(_thumb, ref props);
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

    private static bool IsFullWindow(RegionPreset r)
        => r.X <= 0.0001 && r.Y <= 0.0001 && r.W >= 0.9999 && r.H >= 0.9999;

    // Keeps the crop proportions instead of stretching it over the whole preview.
    private static RECT Letterbox(RECT dest, int srcW, int srcH)
    {
        int dw = dest.Right - dest.Left;
        int dh = dest.Bottom - dest.Top;
        if (dw <= 0 || dh <= 0 || srcW <= 0 || srcH <= 0) return dest;

        double scale = Math.Min((double)dw / srcW, (double)dh / srcH);
        int nw = Math.Max(1, (int)Math.Round(srcW * scale));
        int nh = Math.Max(1, (int)Math.Round(srcH * scale));
        int ox = dest.Left + (dw - nw) / 2;
        int oy = dest.Top + (dh - nh) / 2;
        return new RECT { Left = ox, Top = oy, Right = ox + nw, Bottom = oy + nh };
    }

    public void ApplyRegion(RegionPreset? region)
    {
        _region = region;
        UpdateRegionBadge();
        UpdateThumbnailRect(_isZoomed);
    }

    private void UpdateRegionBadge()
    {
        bool active = _region != null && !IsFullWindow(_region);
        TxtRegion.Text = active ? $"▣ {_region!.Name}" : string.Empty;
        TxtRegion.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    // Reshapes the preview so the cropped area fills it without black bars.
    public void FitToRegion()
    {
        if (_region == null || IsFullWindow(_region)) return;
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
        _owner.SaveLayoutForHwnd(_item.HWnd, Left, Top, Width, Height);
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
        _owner.SaveLayoutForHwnd(_item.HWnd, Left, Top, Width, Height);
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
