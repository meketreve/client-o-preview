using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ClientOPreview.Localization;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview;

public partial class MainWindow : Window
{
    private readonly Dictionary<IntPtr, StreamWindow> _streams = new();

    // UI pages
    private Views.ThumbnailPage _thumbnailPage = null!;
    private Views.HotkeysPage _hotkeysPage = null!;
    private Views.ZoomPage _zoomPage = null!;
    private Views.RegionPage _regionPage = null!;
    private Views.OverlayPage _overlayPage = null!;
    private Views.ClientsPage _clientsPage = null!;
    private Views.LanguagePage _languagePage = null!;
    private Views.AboutPage _aboutPage = null!;

    // Hotkey state
    private const int HOTKEY_CYCLE = 1;
    private const int HOTKEY_DIRECT_BASE = 100; // 100-109 for direct keys
    private int _currentCycleIndex = -1;
    private HwndSource? _hwndSource;

    // State/settings
    private readonly SettingsService _settingsSvc = new();
    private SettingsData _settings = new();

    private bool _previewsTopmost = true;
    private bool _trackLocations = true;
    private bool _uniqueLayout = true;

    private int _thumbWidth = 160;
    private int _thumbHeight = 90;
    private int _opacityPct = 90; // 20..100
    private int _titleFontSize = 12;
    private string _activeHighlightColor = "#2864C8";

    private bool _isExplicitExit = false;
    private RegionPickerWindow? _regionPicker;

    // Previews are currently pushed behind other windows because no client is focused.
    private bool _topmostSuspended = false;

    // Region assigned to a live preview, keyed by the source window handle. The persisted
    // assignment is keyed by window title, which drifts (login screen -> pilot name) and used
    // to make the selection "not stick"; this in-session map always wins while the preview lives.
    private readonly Dictionary<IntPtr, string?> _liveRegions = new();

    private int _staggerCount = 0;
    private readonly DispatcherTimer _fgTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public void ForceClose()
    {
        _isExplicitExit = true;
        this.Close();
    }

    public MainWindow()
    {
        // The language has to be known before InitializeComponent binds the first string.
        _settings = _settingsSvc.GetSettings();
        if (string.IsNullOrWhiteSpace(_settings.Language))
            _settings.Language = Loc.SystemDefault();
        Loc.SetLanguage(_settings.Language);

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
            
            if (_settings.General.MinimizeToTray && !_isExplicitExit)
            {
                e.Cancel = true;
                TrayHelper.MinimizeToTray(this);
            }
            else
            {
                // Real exit: close everything and clean up tray
                TrayHelper.Ensure(this, false);
                _regionPicker?.Close();
                CloseAllStreams();
            }
        };

        var g = _settings.General;
        var t = _settings.Thumbnail;
        _previewsTopmost = g.PreviewsTopmost;
        _trackLocations = g.TrackLocations;
        _uniqueLayout = g.UniqueLayout;
        _thumbWidth = t.Width; _thumbHeight = t.Height; _opacityPct = t.OpacityPct;
        _titleFontSize = t.TitleFontSize;
        _activeHighlightColor = t.ActiveHighlightColor;

        // Instantiate pages
        _thumbnailPage = new Views.ThumbnailPage();
        _thumbnailPage.LoadFrom(t, g);
        _hotkeysPage = new Views.HotkeysPage();
        _hotkeysPage.LoadFrom(_settings.Hotkeys);
        _zoomPage = new Views.ZoomPage();
        _zoomPage.LoadFrom(_settings.Zoom);
        _regionPage = new Views.RegionPage();
        _overlayPage = new Views.OverlayPage();
        _clientsPage = new Views.ClientsPage();
        _languagePage = new Views.LanguagePage();
        _aboutPage = new Views.AboutPage();

        // Wire events (clients)
        _clientsPage.RefreshRequested += (_, __) => RefreshList();
        _clientsPage.OpenStreamsRequested += (_, __) => OpenSelectedStreams();
        _clientsPage.CloseSelectedRequested += (_, __) => CloseSelectedStreams();
        _clientsPage.CloseAllRequested += (_, __) => CloseAllStreams();

        // Wire events (settings that used to live on the General page)
        _thumbnailPage.TrackLocationsChanged += (_, v) => { _trackLocations = v; _settings.General.TrackLocations = v; _settingsSvc.SaveSettings(); };
        _thumbnailPage.UniqueLayoutChanged += (_, v) => { _uniqueLayout = v; _settings.General.UniqueLayout = v; _settingsSvc.SaveSettings(); };
        _thumbnailPage.SnapToGridChanged += (_, v) => { _settings.General.SnapToGrid = v; _settingsSvc.SaveSettings(); UpdateGridSettings(); };
        _thumbnailPage.GridSizeChanged += (_, v) => { _settings.General.GridSize = v; _settingsSvc.SaveSettings(); UpdateGridSettings(); };
        _thumbnailPage.MinimizeToTrayChanged += (_, v) => { _settings.General.MinimizeToTray = v; _settingsSvc.SaveSettings(); };
        _thumbnailPage.TopmostOnlyWhenClientFocusedChanged += (_, v) =>
        {
            _settings.General.TopmostOnlyWhenClientFocused = v;
            _settingsSvc.SaveSettings();
            if (!v) { _topmostSuspended = false; ApplyGlobalTopmost(); }
            else CheckForeground();
        };

        // Wire events (language)
        _languagePage.LanguageSelected += (_, code) =>
        {
            _settings.Language = Loc.Normalize(code);
            Loc.SetLanguage(_settings.Language);
            _settingsSvc.SaveSettings();
        };

        // Wire events (zoom)
        _zoomPage.ZoomChanged += (_, z) =>
        {
            _settings.Zoom = z;
            _settingsSvc.SaveSettings();
            // Trigger update to any active thumbnails if needed
            UpdateZoomSettings();
        };

        // Wire events (region focus)
        _regionPage.RefreshRequested += (_, __) => RefreshRegionPage();
        _regionPage.DefineRequested += (_, entry) =>
        {
            if (_streams.TryGetValue(entry.HWnd, out var win)) OpenRegionPickerFor(win);
        };
        // Build a preset from an example preview without touching the existing ones.
        _regionPage.NewPresetRequested += (_, entry) =>
        {
            if (entry != null && _streams.TryGetValue(entry.HWnd, out var win)) OpenRegionPickerFor(win, newPreset: true);
        };
        _regionPage.AssignRequested += (_, args) =>
        {
            if (!_streams.TryGetValue(args.Stream.HWnd, out var win)) return;
            AssignRegion(args.Stream.HWnd, win, args.Preset, fit: true);
            RefreshRegionPage();
        };
        _regionPage.DeletePresetRequested += (_, name) =>
        {
            _settings.Regions.RemovePreset(name);
            foreach (var hwnd in _liveRegions.Keys.ToList())
            {
                if (string.Equals(_liveRegions[hwnd], name, StringComparison.OrdinalIgnoreCase))
                    _liveRegions[hwnd] = null;
            }
            _settingsSvc.SaveSettings();
            foreach (var kv in _streams) ApplyRegionForStream(kv.Key, kv.Value);
            RefreshRegionPage();
        };

        // Wire events (thumbnail)
        _thumbnailPage.ThumbnailChanged += (_, args) =>
        {
            _thumbWidth = args.Width;
            _thumbHeight = args.Height;
            _opacityPct = args.OpacityPct;
            _titleFontSize = args.TitleFontSize;
            _activeHighlightColor = args.ActiveColor;
            _settings.Thumbnail.ActiveHighlightColor = _activeHighlightColor;
            ApplyThumbnailToStreams();
            _settingsSvc.SaveSettings();
        };
        _thumbnailPage.TopmostChanged += (_, v) =>
        {
            _previewsTopmost = v;
            _settings.General.PreviewsTopmost = v;
            _topmostSuspended = false;
            ApplyGlobalTopmost();
            _settingsSvc.SaveSettings();
        };

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
    private void Nav_Thumbnail(object sender, RoutedEventArgs e) => ContentHost.Content = _thumbnailPage;
    private void Nav_Hotkeys(object sender, RoutedEventArgs e)
    {
        RefreshHotkeysOpenThumbnails();
        ContentHost.Content = _hotkeysPage;
    }
    private void Nav_Zoom(object sender, RoutedEventArgs e) => ContentHost.Content = _zoomPage;
    private void Nav_Region(object sender, RoutedEventArgs e)
    {
        RefreshRegionPage();
        ContentHost.Content = _regionPage;
    }
    private void Nav_Overlay(object sender, RoutedEventArgs e) => ContentHost.Content = _overlayPage;
    private void Nav_Clients(object sender, RoutedEventArgs e) => ContentHost.Content = _clientsPage;
    private void Nav_Language(object sender, RoutedEventArgs e)
    {
        _languagePage.LoadFrom(Loc.CurrentLanguage);
        ContentHost.Content = _languagePage;
    }
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
        var topmost = _previewsTopmost && !_topmostSuspended;
        foreach (var w in _streams.Values)
        {
            w.Topmost = topmost;
        }
    }

    // With "only on top while a client is in focus", the previews sink behind other windows as
    // soon as the user alt-tabs to something that is neither a client nor part of this app.
    private void UpdateTopmostForFocus(IntPtr fg)
    {
        if (!_previewsTopmost)
        {
            _topmostSuspended = false;
            return;
        }
        if (!_settings.General.TopmostOnlyWhenClientFocused)
        {
            if (_topmostSuspended)
            {
                _topmostSuspended = false;
                ApplyGlobalTopmost();
            }
            return;
        }

        var focused = _streams.ContainsKey(fg) || IsOwnAppWindow(fg);
        if (focused != _topmostSuspended) return;   // already in the right state
        _topmostSuspended = !focused;
        ApplyGlobalTopmost();
    }

    private bool IsOwnAppWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        foreach (Window w in System.Windows.Application.Current.Windows)
        {
            if (new WindowInteropHelper(w).Handle == hwnd) return true;
        }
        return false;
    }

    private void ApplyThumbnailToStreams()
    {
        var alpha = Math.Max(0.2, Math.Min(1.0, _opacityPct / 100.0));
        foreach (var w in _streams.Values)
        {
            w.SetOpacity(alpha);
            w.SetSize(_thumbWidth, _thumbHeight);
            w.SetTitleFontSize(_titleFontSize);
            w.SetHighlightColor(_activeHighlightColor);
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

            // Always update active state for highlighting
            foreach (var kv in _streams)
            {
                kv.Value.SetActiveState(kv.Key == fg);
            }

            UpdateTopmostForFocus(fg);
        }
        catch { }
    }

    internal void OnPreviewClicked(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
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
            if (string.IsNullOrEmpty(title)) return "default";
            
            int occIndex = 0;
            if (_streams.TryGetValue(hwnd, out var sw))
            {
                occIndex = sw.OccurrenceIndex;
            }
            
            return $"title:{occIndex}:{SanitizeLayoutKey(title)}";
        }
        return "default";
    }

    private void ApplySavedGeometry(IntPtr hwnd, StreamWindow win, int occurrenceIndex)
    {
        var title = GetWindowTitle(hwnd);
        var sanitizedTitle = SanitizeLayoutKey(title);
        var key = !_trackLocations ? string.Empty : 
                 _uniqueLayout ? $"title:{occurrenceIndex}:{sanitizedTitle}" : "default";

        if (string.IsNullOrEmpty(key)) return;
        
        var g = _settingsSvc.GetLayout(key);
        
        // Migrate old key format (without occurrence index) if new one not found
        if (string.IsNullOrWhiteSpace(g) && occurrenceIndex == 0 && _uniqueLayout)
        {
            var oldKey = $"title:{sanitizedTitle}";
            g = _settingsSvc.GetLayout(oldKey);
            if (!string.IsNullOrWhiteSpace(g))
            {
                // Move to new key and delete old
                _settingsSvc.SetLayout(key, g);
                _settingsSvc.SetLayout(oldKey, ""); // or some way to delete if I added a DeleteLayout method
            }
        }

        if (string.IsNullOrWhiteSpace(g)) 
        {
            // Stagger if no layout found to avoid stacking
            win.Left = 50 + (_staggerCount % 10) * 30;
            win.Top = 50 + (_staggerCount % 10) * 30;
            _staggerCount++;
            return;
        }
        try
        {
            // geometry formato: WIDTHxHEIGHT+LEFT+TOP
            var parts = g.Split('+');
            var wh = parts[0].Split('x');
            int w = int.Parse(wh[0]); int h = int.Parse(wh[1]);
            int left = int.Parse(parts[1]); int top = int.Parse(parts[2]);
            
            win.Left = left; 
            win.Top = top;
            // Set window size directly to restore exact saved state
            win.Width = Math.Max(120, w);
            win.Height = Math.Max(90, h);
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
        var availableWindows = WindowEnumerator.GetTopLevelWindows(selfHwnd).ToList();
        
        foreach (var savedTitle in lastTitles)
        {
            // Find a window that matches the saved title AND isn't already open
            var match = availableWindows.FirstOrDefault(w => w.Title == savedTitle && !_streams.ContainsKey(w.HWnd));
            if (match != null)
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

        // Calculate occurrence index for windows with same title
        int occIndex = _streams.Values.Count(w => w.WindowTitle == item.Title);

        var win = new StreamWindow(this, item)
        {
            Topmost = _previewsTopmost && !_topmostSuspended,
            OccurrenceIndex = occIndex
        };

        // Apply global default size first
        win.SetSize(_thumbWidth, _thumbHeight);
        win.SetOpacity(_opacityPct / 100.0);
        win.SetTitleFontSize(_titleFontSize);
        win.SetHighlightColor(_activeHighlightColor);
        win.ApplyZoomSettings(_settings.Zoom);
        win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);

        // Then apply saved geometry (might override size/position if title matches)
        ApplySavedGeometry(item.HWnd, win, occIndex);

        win.Closed += (_, __) => { _streams.Remove(item.HWnd); _liveRegions.Remove(item.HWnd); };
        _streams[item.HWnd] = win;

        // Region depends on the occurrence index, so it needs the stream registered first.
        ApplyRegionForStream(item.HWnd, win);
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
        
        // Check if there's a mapping for this hotkey index
        if (_settings.Hotkeys.DirectKeyMappings.TryGetValue(index, out var mappedTitle) && !string.IsNullOrEmpty(mappedTitle))
        {
            // Find hwnd by title
            foreach (var kv in _streams)
            {
                var title = GetWindowTitle(kv.Key);
                if (title == mappedTitle)
                {
                    ActivateSourceWindow(kv.Key);
                    return;
                }
            }
        }
        
        // Fallback: activate by index order
        var hwndList = _streams.Keys.ToList();
        if (index >= 0 && index < hwndList.Count)
        {
            var hwnd = hwndList[index];
            ActivateSourceWindow(hwnd);
            _currentCycleIndex = index;
        }
    }

    // ===== Region focus =====

    private string RegionKeyForHwnd(IntPtr hwnd)
    {
        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrEmpty(title)) title = "unknown";
        int occIndex = _streams.TryGetValue(hwnd, out var sw) ? sw.OccurrenceIndex : 0;
        return $"title:{occIndex}:{SanitizeLayoutKey(title)}";
    }

    /// <summary>Preset assigned to a live preview: the in-session map wins over the title key.</summary>
    private string? ResolveRegionName(IntPtr hwnd)
    {
        if (_liveRegions.TryGetValue(hwnd, out var live)) return live;
        return _settings.Regions.Assignments.TryGetValue(RegionKeyForHwnd(hwnd), out var n) ? n : null;
    }

    private void AssignRegion(IntPtr hwnd, StreamWindow win, string? presetName, bool fit)
    {
        _liveRegions[hwnd] = string.IsNullOrEmpty(presetName) ? null : presetName;

        var key = RegionKeyForHwnd(hwnd);
        if (string.IsNullOrEmpty(presetName)) _settings.Regions.Assignments.Remove(key);
        else _settings.Regions.Assignments[key] = presetName;
        _settingsSvc.SaveSettings();

        ApplyRegionForStream(hwnd, win, fit);
    }

    private void ApplyRegionForStream(IntPtr hwnd, StreamWindow win, bool fit = false)
    {
        var preset = _settings.Regions.FindPreset(ResolveRegionName(hwnd));
        win.ApplyRegion(preset);
        if (fit && preset != null) win.FitToRegion();
    }

    internal void OpenRegionPickerFor(StreamWindow win, bool newPreset = false)
    {
        if (_regionPicker != null)
        {
            _regionPicker.Activate();
            return;
        }

        var hwnd = win.SourceHwnd;
        // A new preset starts from a blank name so an existing one can never be overwritten.
        var existing = newPreset ? null : _settings.Regions.FindPreset(ResolveRegionName(hwnd));
        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrEmpty(title)) title = win.WindowTitle;

        var picker = new RegionPickerWindow(hwnd, title, existing, suggestNameFromTitle: !newPreset)
        {
            IsNameTaken = name =>
                !string.Equals(name, existing?.Name, StringComparison.OrdinalIgnoreCase)
                && _settings.Regions.FindPreset(name) != null
        };
        picker.RegionSaved += (_, result) =>
        {
            _settings.Regions.UpsertPreset(result.Preset);
            AssignRegion(hwnd, win, result.Preset.Name, fit: result.FitPreview);
            RefreshRegionPage();
        };
        picker.Closed += (_, __) => _regionPicker = null;
        _regionPicker = picker;
        picker.Show();
    }

    private void RefreshRegionPage()
    {
        var entries = new List<Views.StreamEntry>();
        foreach (var kv in _streams)
        {
            var key = RegionKeyForHwnd(kv.Key);
            var title = GetWindowTitle(kv.Key);
            if (string.IsNullOrEmpty(title)) title = kv.Value.WindowTitle;
            if (kv.Value.OccurrenceIndex > 0) title = $"{title}  #{kv.Value.OccurrenceIndex + 1}";

            entries.Add(new Views.StreamEntry
            {
                HWnd = kv.Key,
                Key = key,
                Title = title,
                AssignedPreset = ResolveRegionName(kv.Key)
            });
        }

        _regionPage.SetPresets(_settings.Regions.Presets);
        _regionPage.SetStreams(entries);
    }

    private void UpdateZoomSettings()
    {
        foreach (var win in _streams.Values)
        {
            win.ApplyZoomSettings(_settings.Zoom);
        }
    }

    private void UpdateGridSettings()
    {
        foreach (var win in _streams.Values)
        {
            win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);
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
