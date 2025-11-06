using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview;

public partial class MainWindow : Window
{
    private readonly Dictionary<IntPtr, StreamWindow> _streams = new();

    // UI pages
    private Views.GeneralPage _generalPage = null!;
    private Views.ThumbnailPage _thumbnailPage = null!;
    private Views.ZoomPage _zoomPage = null!;
    private Views.OverlayPage _overlayPage = null!;
    private Views.ClientsPage _clientsPage = null!;
    private Views.AboutPage _aboutPage = null!;

    // State/settings
    private readonly SettingsService _settingsSvc = new();
    private SettingsData _settings = new();

    private bool _minimizeInactive = false;
    private bool _reorderInactive = true;
    private bool _previewsTopmost = true;
    private bool _trackLocations = true;
    private bool _hideActivePreview = true;
    private bool _hideWhenNotActive = false;
    private bool _uniqueLayout = true;

    private int _thumbWidth = 384;
    private int _thumbHeight = 216;
    private int _opacityPct = 90; // 20..100

    private readonly DispatcherTimer _fgTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public MainWindow()
    {
        InitializeComponent();

        this.StateChanged += (_, __) =>
        {
            if (_settings.General.MinimizeToTray && this.WindowState == WindowState.Minimized)
            {
                TrayHelper.MinimizeToTray(this);
            }
        };
        this.Closing += (s, e) =>
        {
            if (_settings.General.MinimizeToTray)
            {
                e.Cancel = true;
                TrayHelper.MinimizeToTray(this);
            }
        };

        // Load settings
        _settings = _settingsSvc.Load();
        var g = _settings.General;
        var t = _settings.Thumbnail;
        _minimizeInactive = g.MinimizeInactive;
        _reorderInactive = g.ReorderInactive;
        _previewsTopmost = g.PreviewsTopmost;
        _trackLocations = g.TrackLocations;
        _hideActivePreview = g.HideActivePreview;
        _hideWhenNotActive = g.HideWhenNotActive;
        _uniqueLayout = g.UniqueLayout;
        _thumbWidth = t.Width; _thumbHeight = t.Height; _opacityPct = t.OpacityPct;

        // Instantiate pages
        _generalPage = new Views.GeneralPage();
        _generalPage.LoadFrom(g);
        _thumbnailPage = new Views.ThumbnailPage(_thumbWidth, _thumbHeight, _opacityPct, _previewsTopmost);
        _zoomPage = new Views.ZoomPage();
        _overlayPage = new Views.OverlayPage();
        _clientsPage = new Views.ClientsPage();
        _aboutPage = new Views.AboutPage();

        // Wire events (clients)
        _clientsPage.RefreshRequested += (_, __) => RefreshList();
        _clientsPage.OpenStreamsRequested += (_, __) => OpenSelectedStreams();

        // Wire events (general)
        _generalPage.PreviewsTopmostChanged += (_, v) => { _previewsTopmost = v; _settings.General.PreviewsTopmost = v; ApplyGlobalTopmost(); _settingsSvc.Save(_settings); };
        _generalPage.MinimizeInactiveChanged += (_, v) => { _minimizeInactive = v; _settings.General.MinimizeInactive = v; _settingsSvc.Save(_settings); };
        _generalPage.ReorderInactiveChanged += (_, v) => { _reorderInactive = v; _settings.General.ReorderInactive = v; _settingsSvc.Save(_settings); };
        _generalPage.HideActivePreviewChanged += (_, v) => { _hideActivePreview = v; _settings.General.HideActivePreview = v; _settingsSvc.Save(_settings); };
        _generalPage.HideWhenNotActiveChanged += (_, v) => { _hideWhenNotActive = v; _settings.General.HideWhenNotActive = v; _settingsSvc.Save(_settings); };
        _generalPage.TrackLocationsChanged += (_, v) => { _trackLocations = v; _settings.General.TrackLocations = v; _settingsSvc.Save(_settings); };
        _generalPage.UniqueLayoutChanged += (_, v) => { _uniqueLayout = v; _settings.General.UniqueLayout = v; _settingsSvc.Save(_settings); };
        _generalPage.MinimizeToTrayChanged += (_, v) => { _settings.General.MinimizeToTray = v; _settingsSvc.Save(_settings); };

        // Wire events (thumbnail)
        _thumbnailPage.ThumbnailChanged += (_, args) =>
        {
            _thumbWidth = args.Width;
            _thumbHeight = args.Height;
            _opacityPct = args.OpacityPct;
            _settings.Thumbnail.Width = _thumbWidth;
            _settings.Thumbnail.Height = _thumbHeight;
            _settings.Thumbnail.OpacityPct = _opacityPct;
            ApplyThumbnailToStreams();
            _settingsSvc.Save(_settings);
        };
        _thumbnailPage.TopmostChanged += (_, v) => { _previewsTopmost = v; _settings.General.PreviewsTopmost = v; ApplyGlobalTopmost(); _settingsSvc.Save(_settings); };

        // Default page
        ContentHost.Content = _clientsPage;
        Loaded += (_, __) => RefreshList();

        // Foreground timer behavior
        _fgTimer.Tick += (_, __) => CheckForeground();
        _fgTimer.Start();
    }

    // Navigation handlers
    private void Nav_General(object sender, RoutedEventArgs e) => ContentHost.Content = _generalPage;
    private void Nav_Thumbnail(object sender, RoutedEventArgs e) => ContentHost.Content = _thumbnailPage;
    private void Nav_Zoom(object sender, RoutedEventArgs e) => ContentHost.Content = _zoomPage;
    private void Nav_Overlay(object sender, RoutedEventArgs e) => ContentHost.Content = _overlayPage;
    private void Nav_Clients(object sender, RoutedEventArgs e) => ContentHost.Content = _clientsPage;
    private void Nav_About(object sender, RoutedEventArgs e) => ContentHost.Content = _aboutPage;

    private void RefreshList()
    {
        var selfHwnd = new WindowInteropHelper(this).Handle;
        var items = WindowEnumerator.GetTopLevelWindows(selfHwnd);
        _clientsPage.SetWindows(items);
    }

    private void OpenSelectedStreams()
    {
        var selected = _clientsPage.SelectedWindows.ToList();
        foreach (var item in selected)
        {
            if (_streams.ContainsKey(item.HWnd))
            {
                _streams[item.HWnd].Activate();
                continue;
            }
            var win = new StreamWindow(this, item)
            {
                Topmost = _previewsTopmost
            };
            ApplySavedGeometry(item.HWnd, win);
            win.SetOpacity(_opacityPct / 100.0);
            win.SetSize(_thumbWidth, _thumbHeight);
            win.Closed += (_, __) => _streams.Remove(item.HWnd);
            _streams[item.HWnd] = win;
            win.Show();
        }
    }

    private void ApplyGlobalTopmost()
    {
        foreach (var w in _streams.Values)
        {
            w.Topmost = _previewsTopmost;
        }
    }

    private void ApplyThumbnailToStreams()
    {
        var alpha = Math.Max(0.2, Math.Min(1.0, _opacityPct / 100.0));
        foreach (var w in _streams.Values)
        {
            w.SetOpacity(alpha);
            w.SetSize(_thumbWidth, _thumbHeight);
        }
    }

    private void CheckForeground()
    {
        try
        {
            var fg = GetForegroundWindow();
            var tracked = new HashSet<IntPtr>(_streams.Keys);
            var thisHwnd = new WindowInteropHelper(this).Handle;
            if (_hideWhenNotActive)
            {
                if (!tracked.Contains(fg) && fg != thisHwnd)
                {
                    foreach (var w in _streams.Values) w.Hide();
                }
                else
                {
                    foreach (var w in _streams.Values) w.Show();
                }
            }
            if (_hideActivePreview)
            {
                foreach (var kv in _streams)
                {
                    if (kv.Key == fg) kv.Value.Hide(); else kv.Value.Show();
                }
            }
        }
        catch { }
    }

    internal void OnPreviewClicked(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);

        if (_minimizeInactive)
        {
            foreach (var kv in _streams)
            {
                if (kv.Key == hwnd) continue;
                ShowWindow(kv.Key, SW_MINIMIZE);
            }
        }
        else if (_reorderInactive)
        {
            // Bring active to top; send others to back
            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            foreach (var kv in _streams)
            {
                if (kv.Key == hwnd) continue;
                SetWindowPos(kv.Key, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
    }

    // ===== Layout persistence =====
    private string LayoutKeyForHwnd(IntPtr hwnd)
    {
        if (!_trackLocations) return string.Empty;
        if (_uniqueLayout)
        {
            try { GetWindowThreadProcessId(hwnd, out var pid); return pid != 0 ? $"pid:{pid}" : $"hwnd:{hwnd.ToInt64()}"; }
            catch { return $"hwnd:{hwnd.ToInt64()}"; }
        }
        return "default";
    }

    private void ApplySavedGeometry(IntPtr hwnd, StreamWindow win)
    {
        if (!_trackLocations) return;
        var key = LayoutKeyForHwnd(hwnd);
        if (string.IsNullOrEmpty(key)) return;
        var g = _settingsSvc.GetLayout(key);
        if (string.IsNullOrWhiteSpace(g)) return;
        try
        {
            // geometry formato: WIDTHxHEIGHT+LEFT+TOP
            var parts = g.Split('+');
            var wh = parts[0].Split('x');
            int w = int.Parse(wh[0]); int h = int.Parse(wh[1]);
            int left = int.Parse(parts[1]); int top = int.Parse(parts[2]);
            win.Left = left; win.Top = top; win.SetSize(w, h);
        }
        catch { }
    }

    internal void SaveLayoutForHwnd(IntPtr hwnd, double left, double top, double width, double height)
    {
        if (!_trackLocations) return;
        var key = LayoutKeyForHwnd(hwnd);
        if (string.IsNullOrEmpty(key)) return;
        var w = Math.Max(1, (int)Math.Round(width));
        var h = Math.Max(1, (int)Math.Round(height));
        var l = (int)Math.Round(left);
        var t = (int)Math.Round(top);
        var geom = $"{w}x{h}+{l}+{t}";
        _settingsSvc.SetLayout(key, geom);
    }
}
