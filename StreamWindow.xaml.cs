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

    public string WindowTitle => _item.Title;
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

    private void UpdateThumbnailRect()
    {
        if (_thumb == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        int h = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY) - 36); // desconta barra superior aproximada
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY,
            rcDestination = new RECT { Left = 0, Top = 32, Right = w, Bottom = 32 + h },
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false
        };
        DwmUpdateThumbnailProperties(_thumb, ref props);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbnailRect();
        _owner.SaveLayoutForHwnd(_item.HWnd, Left, Top, Width, Height);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _owner.SaveLayoutForHwnd(_item.HWnd, Left, Top, Width, Height);
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
