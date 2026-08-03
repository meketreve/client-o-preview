using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClientOPreview.Models;
using static ClientOPreview.Native.NativeMethods;
// WinForms is enabled in this project, so pin the WPF types.
using Point = System.Windows.Point;

namespace ClientOPreview;

public class RegionPickerResult
{
    public RegionPreset Preset { get; set; } = new();
    public bool FitPreview { get; set; }
}

// Live picker: shows the monitored window through a DWM thumbnail and lets the user
// draw the slice they actually care about (drone panel, capacitor, minimap, ...).
public partial class RegionPickerWindow : Window
{
    private readonly IntPtr _source;

    private IntPtr _thumbFull = IntPtr.Zero;
    private IntPtr _thumbCrop = IntPtr.Zero;
    private SIZE _srcSize;

    private RegionOverlayWindow? _overlay;
    private Rect _sel = new(0.25, 0.25, 0.5, 0.5);   // normalized
    private Rect _previewRectDip = Rect.Empty;

    public event EventHandler<RegionPickerResult>? RegionSaved;

    public RegionPickerWindow(IntPtr source, string sourceTitle, RegionPreset? existing)
    {
        _source = source;
        InitializeComponent();

        TxtSource.Text = sourceTitle;
        if (existing != null)
        {
            TxtName.Text = existing.Name;
            ChkLockAspect.IsChecked = existing.LockAspect;
            _sel = new Rect(existing.X, existing.Y, Math.Max(0.01, existing.W), Math.Max(0.01, existing.H));
        }
        else
        {
            TxtName.Text = sourceTitle.Length > 40 ? sourceTitle[..40] : sourceTitle;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var dest = new WindowInteropHelper(this).Handle;
        if (DwmRegisterThumbnail(dest, _source, out _thumbFull) != 0) _thumbFull = IntPtr.Zero;
        if (DwmRegisterThumbnail(dest, _source, out _thumbCrop) != 0) _thumbCrop = IntPtr.Zero;

        if (_thumbFull != IntPtr.Zero) DwmQueryThumbnailSourceSize(_thumbFull, out _srcSize);

        _overlay = new RegionOverlayWindow { Owner = this };
        _overlay.SelectionChanged += (_, norm) =>
        {
            _sel = norm;
            UpdateReadout();
            RefreshResult();
        };
        _overlay.KeyPressed += (_, key) =>
        {
            if (key == Key.Escape) Close();
            else if (key == Key.Enter) Save();
        };
        _overlay.Show();

        UpdateLayout();
        RefreshPreview();
    }

    // ===== Layout / DWM =====

    private void RefreshPreview()
    {
        if (_srcSize.cx <= 0 || _srcSize.cy <= 0) return;

        _previewRectDip = ComputeFitRect(PreviewHost, _srcSize.cx, _srcSize.cy);
        if (_thumbFull != IntPtr.Zero && !_previewRectDip.IsEmpty)
        {
            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY,
                rcDestination = ToPixels(_previewRectDip),
                opacity = 255,
                fVisible = true,
                fSourceClientAreaOnly = false
            };
            DwmUpdateThumbnailProperties(_thumbFull, ref props);
        }

        SyncOverlay();
        RefreshResult();
        UpdateReadout();
    }

    private void RefreshResult()
    {
        if (_thumbCrop == IntPtr.Zero || _srcSize.cx <= 0 || _srcSize.cy <= 0) return;

        var src = SourceRect();
        int cw = src.Right - src.Left;
        int ch = src.Bottom - src.Top;
        var rect = ComputeFitRect(ResultHost, cw, ch);
        if (rect.IsEmpty) return;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE | DWM_TNP_VISIBLE | DWM_TNP_OPACITY,
            rcDestination = ToPixels(rect),
            rcSource = src,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false
        };
        DwmUpdateThumbnailProperties(_thumbCrop, ref props);
    }

    private Rect ComputeFitRect(FrameworkElement host, double srcW, double srcH)
    {
        if (srcW <= 0 || srcH <= 0 || !host.IsVisible) return Rect.Empty;

        double aw = host.ActualWidth - 2;   // 1px border on each side
        double ah = host.ActualHeight - 2;
        if (aw <= 0 || ah <= 0) return Rect.Empty;

        Point origin;
        try { origin = host.TransformToAncestor(this).Transform(new Point(0, 0)); }
        catch { return Rect.Empty; }

        double scale = Math.Min(aw / srcW, ah / srcH);
        double w = srcW * scale;
        double h = srcH * scale;
        return new Rect(origin.X + 1 + (aw - w) / 2, origin.Y + 1 + (ah - h) / 2, w, h);
    }

    private RECT ToPixels(Rect r)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new RECT
        {
            Left = (int)Math.Round(r.X * dpi.DpiScaleX),
            Top = (int)Math.Round(r.Y * dpi.DpiScaleY),
            Right = (int)Math.Round((r.X + r.Width) * dpi.DpiScaleX),
            Bottom = (int)Math.Round((r.Y + r.Height) * dpi.DpiScaleY)
        };
    }

    private RECT SourceRect()
    {
        int sl = Clamp((int)Math.Round(_sel.X * _srcSize.cx), 0, _srcSize.cx - 1);
        int st = Clamp((int)Math.Round(_sel.Y * _srcSize.cy), 0, _srcSize.cy - 1);
        int sr = Clamp((int)Math.Round((_sel.X + _sel.Width) * _srcSize.cx), sl + 1, _srcSize.cx);
        int sb = Clamp((int)Math.Round((_sel.Y + _sel.Height) * _srcSize.cy), st + 1, _srcSize.cy);
        return new RECT { Left = sl, Top = st, Right = sr, Bottom = sb };
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

    private void SyncOverlay()
    {
        if (_overlay == null || _previewRectDip.IsEmpty) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        Point screenPx;
        try { screenPx = PointToScreen(new Point(_previewRectDip.X, _previewRectDip.Y)); }
        catch { return; }

        _overlay.Left = screenPx.X / dpi.DpiScaleX;
        _overlay.Top = screenPx.Y / dpi.DpiScaleY;
        _overlay.Width = _previewRectDip.Width;
        _overlay.Height = _previewRectDip.Height;
        _overlay.SetNormalizedSelection(_sel);
    }

    private void UpdateReadout()
    {
        var src = SourceRect();
        TxtReadout.Text = $"X {_sel.X:P0} · Y {_sel.Y:P0} · W {_sel.Width:P0} · H {_sel.Height:P0}" +
                          $"\n{src.Right - src.Left} × {src.Bottom - src.Top} px of {_srcSize.cx} × {_srcSize.cy}";
    }

    // ===== Events =====

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshPreview();

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SyncOverlay();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (_overlay == null) return;
        if (WindowState == WindowState.Minimized) _overlay.Hide();
        else { _overlay.Show(); Dispatcher.BeginInvoke(new Action(RefreshPreview)); }
    }

    private void OnQuickAnchor(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        var parts = tag.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int col) || !int.TryParse(parts[1], out int row)) return;

        double size = SliderAnchorSize.Value;
        _sel = new Rect(col * (1 - size) / 2, row * (1 - size) / 2, size, size);
        SyncOverlay();
        RefreshResult();
        UpdateReadout();
    }

    private void OnFullWindow(object sender, RoutedEventArgs e)
    {
        _sel = new Rect(0, 0, 1, 1);
        SyncOverlay();
        RefreshResult();
        UpdateReadout();
    }

    private void OnSave(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            System.Windows.MessageBox.Show(this, "Give the preset a name (e.g. the pilot name).",
                "Region Focus", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtName.Focus();
            return;
        }

        RegionSaved?.Invoke(this, new RegionPickerResult
        {
            Preset = new RegionPreset
            {
                Name = name,
                X = Math.Round(_sel.X, 4),
                Y = Math.Round(_sel.Y, 4),
                W = Math.Round(_sel.Width, 4),
                H = Math.Round(_sel.Height, 4),
                LockAspect = ChkLockAspect.IsChecked == true
            },
            FitPreview = ChkFitWindow.IsChecked == true
        });
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void Window_Closed(object sender, EventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
        }
        if (_thumbFull != IntPtr.Zero) { DwmUnregisterThumbnail(_thumbFull); _thumbFull = IntPtr.Zero; }
        if (_thumbCrop != IntPtr.Zero) { DwmUnregisterThumbnail(_thumbCrop); _thumbCrop = IntPtr.Zero; }
    }
}
