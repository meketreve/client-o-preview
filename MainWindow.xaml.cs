using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private Views.HotkeysPage _hotkeysPage = null!;
    private Views.ZoomPage _zoomPage = null!;
    private Views.OverlayPage _overlayPage = null!;
    private Views.ClientsPage _clientsPage = null!;
    private Views.AboutPage _aboutPage = null!;

    // Hotkey state
    private const int HOTKEY_CYCLE = 1;
    private const int HOTKEY_DIRECT_BASE = 100; // 100-109 for direct keys
    private int _currentCycleIndex = -1;
    private HwndSource? _hwndSource;

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
            // Save which windows are open before closing
            SaveOpenWindowTitles();
            
            if (_settings.General.MinimizeToTray)
            {
                e.Cancel = true;
                TrayHelper.MinimizeToTray(this);
            }
            else
            {
                // Fechar todas as thumbnails ao fechar o app
                CloseAllStreams();
            }
        };

        // Load settings
        _settings = _settingsSvc.GetSettings();
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
        _hotkeysPage = new Views.HotkeysPage();
        _hotkeysPage.LoadFrom(_settings.Hotkeys);
        _zoomPage = new Views.ZoomPage();
        _overlayPage = new Views.OverlayPage();
        _clientsPage = new Views.ClientsPage();
        _aboutPage = new Views.AboutPage();

        // Wire events (clients)
        _clientsPage.RefreshRequested += (_, __) => RefreshList();
        _clientsPage.OpenStreamsRequested += (_, __) => OpenSelectedStreams();
        _clientsPage.CloseSelectedRequested += (_, __) => CloseSelectedStreams();
        _clientsPage.CloseAllRequested += (_, __) => CloseAllStreams();

        // Wire events (general)
        _generalPage.PreviewsTopmostChanged += (_, v) => { _previewsTopmost = v; _settings.General.PreviewsTopmost = v; ApplyGlobalTopmost(); _settingsSvc.SaveSettings(); };
        _generalPage.MinimizeInactiveChanged += (_, v) => { _minimizeInactive = v; _settings.General.MinimizeInactive = v; _settingsSvc.SaveSettings(); };
        _generalPage.ReorderInactiveChanged += (_, v) => { _reorderInactive = v; _settings.General.ReorderInactive = v; _settingsSvc.SaveSettings(); };
        _generalPage.HideActivePreviewChanged += (_, v) => { _hideActivePreview = v; _settings.General.HideActivePreview = v; _settingsSvc.SaveSettings(); };
        _generalPage.HideWhenNotActiveChanged += (_, v) => { _hideWhenNotActive = v; _settings.General.HideWhenNotActive = v; _settingsSvc.SaveSettings(); };
        _generalPage.TrackLocationsChanged += (_, v) => { _trackLocations = v; _settings.General.TrackLocations = v; _settingsSvc.SaveSettings(); };
        _generalPage.UniqueLayoutChanged += (_, v) => { _uniqueLayout = v; _settings.General.UniqueLayout = v; _settingsSvc.SaveSettings(); };
        _generalPage.MinimizeToTrayChanged += (_, v) => { _settings.General.MinimizeToTray = v; _settingsSvc.SaveSettings(); };

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
            _settingsSvc.SaveSettings();
        };
        _thumbnailPage.TopmostChanged += (_, v) => { _previewsTopmost = v; _settings.General.PreviewsTopmost = v; ApplyGlobalTopmost(); _settingsSvc.SaveSettings(); };

        // Wire events (hotkeys)
        _hotkeysPage.HotkeysChanged += (_, hk) =>
        {
            _settings.Hotkeys = hk;
            _settingsSvc.SaveSettings();
            ReregisterHotkeys();
        };

        // Default page
        ContentHost.Content = _clientsPage;
        Loaded += (_, __) =>
        {
            RefreshList();
            ReopenLastWindows();
            SetupHotkeys();
        };

        Closed += (_, __) => UnregisterAllHotkeys();

        // Foreground timer behavior
        _fgTimer.Tick += (_, __) => CheckForeground();
        _fgTimer.Start();
    }

    // Navigation handlers
    private void Nav_General(object sender, RoutedEventArgs e) => ContentHost.Content = _generalPage;
    private void Nav_Thumbnail(object sender, RoutedEventArgs e) => ContentHost.Content = _thumbnailPage;
    private void Nav_Hotkeys(object sender, RoutedEventArgs e)
    {
        RefreshHotkeysOpenThumbnails();
        ContentHost.Content = _hotkeysPage;
    }
    private void Nav_Zoom(object sender, RoutedEventArgs e) => ContentHost.Content = _zoomPage;
    private void Nav_Overlay(object sender, RoutedEventArgs e) => ContentHost.Content = _overlayPage;
    private void Nav_Clients(object sender, RoutedEventArgs e) => ContentHost.Content = _clientsPage;
    private void Nav_About(object sender, RoutedEventArgs e) => ContentHost.Content = _aboutPage;

    private void RefreshHotkeysOpenThumbnails()
    {
        var titles = _streams.Keys.Select(hwnd => GetWindowTitle(hwnd)).Where(t => !string.IsNullOrEmpty(t));
        _hotkeysPage.UpdateOpenThumbnails(titles);
    }

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
            OpenStreamForItem(item);
        }
    }

    private void CloseSelectedStreams()
    {
        var selected = _clientsPage.SelectedWindows.ToList();
        foreach (var item in selected)
        {
            if (_streams.TryGetValue(item.HWnd, out var win))
            {
                win.Close();
                _streams.Remove(item.HWnd);
            }
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
            // Verificar e fechar thumbnails de janelas que não existem mais
            var toClose = new List<IntPtr>();
            foreach (var hwnd in _streams.Keys)
            {
                if (!IsWindow(hwnd))
                {
                    toClose.Add(hwnd);
                }
            }
            foreach (var hwnd in toClose)
            {
                if (_streams.TryGetValue(hwnd, out var win))
                {
                    win.Close();
                    _streams.Remove(hwnd);
                }
            }

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
    
    private string GetWindowTitle(IntPtr hwnd)
    {
        try
        {
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }
        catch { return string.Empty; }
    }
    
    private string SanitizeLayoutKey(string title)
    {
        // Remove characters that could cause issues in JSON keys
        var invalid = new[] { '"', '\\', '/', '\n', '\r', '\t' };
        var result = new StringBuilder();
        foreach (var c in title)
        {
            if (!invalid.Contains(c))
                result.Append(c);
        }
        return result.ToString().Trim();
    }
    
    private string LayoutKeyForHwnd(IntPtr hwnd)
    {
        if (!_trackLocations) return string.Empty;
        if (_uniqueLayout)
        {
            var title = GetWindowTitle(hwnd);
            if (!string.IsNullOrEmpty(title))
            {
                return $"title:{SanitizeLayoutKey(title)}";
            }
            return $"hwnd:{hwnd.ToInt64()}";
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
    
    private void SaveOpenWindowTitles()
    {
        var titles = new List<string>();
        foreach (var kv in _streams)
        {
            var title = GetWindowTitle(kv.Key);
            if (!string.IsNullOrEmpty(title) && !titles.Contains(title))
            {
                titles.Add(title);
            }
        }
        _settingsSvc.SetLastOpenWindows(titles);
    }
    
    private void ReopenLastWindows()
    {
        var lastTitles = _settingsSvc.GetLastOpenWindows();
        if (lastTitles.Count == 0) return;
        
        var selfHwnd = new WindowInteropHelper(this).Handle;
        var availableWindows = WindowEnumerator.GetTopLevelWindows(selfHwnd);
        
        foreach (var savedTitle in lastTitles)
        {
            // Find a window that matches the saved title
            var match = availableWindows.FirstOrDefault(w => w.Title == savedTitle);
            if (match != null && !_streams.ContainsKey(match.HWnd))
            {
                OpenStreamForItem(match);
            }
        }
    }
    
    private void OpenStreamForItem(WindowItem item)
    {
        if (_streams.ContainsKey(item.HWnd))
        {
            _streams[item.HWnd].Activate();
            return;
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

    private void CloseAllStreams()
    {
        SaveOpenWindowTitles();
        foreach (var win in _streams.Values.ToList())
        {
            win.Close();
        }
        _streams.Clear();
    }

    // ===== Hotkey System =====
    
    private void SetupHotkeys()
    {
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(HwndHook);
        RegisterHotkeysFromSettings();
    }

    private void RegisterHotkeysFromSettings()
    {
        if (!_settings.Hotkeys.Enabled) return;
        
        var hWnd = new WindowInteropHelper(this).Handle;
        
        // Register cycle hotkey
        var cycleMods = ParseModifiers(_settings.Hotkeys.CycleModifiers);
        var cycleVk = KeyToVirtualKey(_settings.Hotkeys.CycleKey);
        if (cycleVk != 0)
        {
            RegisterHotKey(hWnd, HOTKEY_CYCLE, cycleMods | MOD_NOREPEAT, cycleVk);
        }
        
        // Register direct hotkeys
        var directMods = ParseModifiers(_settings.Hotkeys.DirectModifiers);
        for (int i = 0; i < _settings.Hotkeys.DirectKeys.Count && i < 10; i++)
        {
            var vk = KeyToVirtualKey(_settings.Hotkeys.DirectKeys[i]);
            if (vk != 0)
            {
                RegisterHotKey(hWnd, HOTKEY_DIRECT_BASE + i, directMods | MOD_NOREPEAT, vk);
            }
        }
    }

    private void UnregisterAllHotkeys()
    {
        var hWnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hWnd, HOTKEY_CYCLE);
        for (int i = 0; i < 10; i++)
        {
            UnregisterHotKey(hWnd, HOTKEY_DIRECT_BASE + i);
        }
    }

    private void ReregisterHotkeys()
    {
        UnregisterAllHotkeys();
        RegisterHotkeysFromSettings();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_CYCLE)
            {
                CycleToNextThumbnail();
                handled = true;
            }
            else if (id >= HOTKEY_DIRECT_BASE && id < HOTKEY_DIRECT_BASE + 10)
            {
                int index = id - HOTKEY_DIRECT_BASE;
                ActivateThumbnailByIndex(index);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void CycleToNextThumbnail()
    {
        if (_streams.Count == 0) return;
        
        var hwndList = _streams.Keys.ToList();
        _currentCycleIndex = (_currentCycleIndex + 1) % hwndList.Count;
        var hwnd = hwndList[_currentCycleIndex];
        
        ActivateSourceWindow(hwnd);
    }

    private void ActivateThumbnailByIndex(int index)
    {
        if (_streams.Count == 0) return;
        
        var hwndList = _streams.Keys.ToList();
        if (index >= 0 && index < hwndList.Count)
        {
            var hwnd = hwndList[index];
            ActivateSourceWindow(hwnd);
            _currentCycleIndex = index;
        }
    }

    private void ActivateSourceWindow(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        OnPreviewClicked(hwnd);
    }

    private uint ParseModifiers(string modifiers)
    {
        uint mods = MOD_NONE;
        if (string.IsNullOrEmpty(modifiers) || modifiers == "None") return mods;
        
        if (modifiers.Contains("Alt")) mods |= MOD_ALT;
        if (modifiers.Contains("Ctrl")) mods |= MOD_CONTROL;
        if (modifiers.Contains("Shift")) mods |= MOD_SHIFT;
        if (modifiers.Contains("Win")) mods |= MOD_WIN;
        
        return mods;
    }

    private uint KeyToVirtualKey(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return 0;
        
        try
        {
            if (Enum.TryParse<Key>(keyName, true, out var key))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            }
        }
        catch { }
        
        return 0;
    }
}
