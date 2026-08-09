using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using ClientOPreview.Localization;
using ClientOPreview.Models;
using ClientOPreview.Services;

namespace ClientOPreview;

/// <summary>
/// Shell of the app: owns the settings, builds the collaborators and wires the settings pages
/// to them. Every page exposes `event` + `LoadFrom(...)` and never touches settings directly —
/// this class applies the effect and asks for a save.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SettingsService _settingsSvc = new();
    private readonly SettingsData _settings;

    private readonly StreamManager _streams;
    private readonly LayoutStore _layouts;
    private readonly RegionCoordinator _regions;
    private readonly HotkeyManager _hotkeys;

    private readonly Views.ThumbnailPage _thumbnailPage = new();
    private readonly Views.HotkeysPage _hotkeysPage = new();
    private readonly Views.ZoomPage _zoomPage = new();
    private readonly Views.RegionPage _regionPage = new();
    private readonly Views.ClientsPage _clientsPage = new();
    private readonly Views.LanguagePage _languagePage = new();
    private readonly Views.AboutPage _aboutPage = new();

    private bool _isExplicitExit;

    public MainWindow()
    {
        // The language has to be known before InitializeComponent binds the first string.
        _settings = _settingsSvc.GetSettings();
        if (string.IsNullOrWhiteSpace(_settings.Language)) _settings.Language = Loc.SystemDefault();
        Loc.SetLanguage(_settings.Language);

        InitializeComponent();

        _layouts = new LayoutStore(_settingsSvc);
        _streams = new StreamManager(this, _settings);
        _regions = new RegionCoordinator(_settingsSvc, _streams);
        _hotkeys = new HotkeyManager(this);

        WireCollaborators();
        WirePages();
        LoadPages();

        ContentHost.Content = _clientsPage;

        Loaded += (_, __) =>
        {
            RefreshList();
            ReopenLastWindows();
            _hotkeys.Attach(_settings.Hotkeys);
            _hotkeysPage.ShowRegistrationResult(_hotkeys.FailedCombos);
        };
        Closed += (_, __) => _hotkeys.Dispose();
        StateChanged += (_, __) =>
        {
            if (_settings.General.MinimizeToTray && WindowState == WindowState.Minimized)
                TrayHelper.MinimizeToTray(this);
        };
        Closing += OnClosing;

        _streams.Start();
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }

    // ===== Wiring =====

    private void WireCollaborators()
    {
        _streams.Opening += (_, win) =>
        {
            _layouts.Apply(win);
            _regions.Apply(win);
        };
        _streams.StreamClosed += (_, hwnd) => _regions.Forget(hwnd);

        _regions.Changed += (_, __) => RefreshRegionPage();

        _hotkeys.CycleRequested += (_, __) => _streams.CycleNext();
        _hotkeys.DirectRequested += (_, index) => _streams.ActivateByIndex(index);
    }

    private void WirePages()
    {
        _clientsPage.RefreshRequested += (_, __) => RefreshList();
        _clientsPage.OpenStreamsRequested += (_, __) =>
        {
            foreach (var item in _clientsPage.SelectedWindows.ToList()) _streams.Open(item);
        };
        _clientsPage.CloseSelectedRequested += (_, __) => _streams.Close(_clientsPage.SelectedWindows);
        _clientsPage.CloseAllRequested += (_, __) =>
        {
            _layouts.RememberOpenWindows(_streams.Handles);
            _streams.CloseAll();
        };

        _thumbnailPage.ThumbnailChanged += (_, args) =>
        {
            var t = _settings.Thumbnail;
            t.Width = args.Width;
            t.Height = args.Height;
            t.OpacityPct = args.OpacityPct;
            t.TitleFontSize = args.TitleFontSize;
            t.ActiveHighlightColor = args.ActiveColor;
            _streams.ApplyThumbnailSettings();
            _settingsSvc.SaveSettings();
        };
        _thumbnailPage.TopmostChanged += (_, v) => SaveGeneral(g => g.PreviewsTopmost = v, _streams.ResumeTopmost);
        _thumbnailPage.TopmostOnlyWhenClientFocusedChanged += (_, v) => SaveGeneral(
            g => g.TopmostOnlyWhenClientFocused = v,
            () => { if (v) _streams.CheckForeground(); else _streams.ResumeTopmost(); });
        _thumbnailPage.MinimizeToTrayChanged += (_, v) => SaveGeneral(g => g.MinimizeToTray = v);
        _thumbnailPage.TrackLocationsChanged += (_, v) => SaveGeneral(g => g.TrackLocations = v);
        _thumbnailPage.UniqueLayoutChanged += (_, v) => SaveGeneral(g => g.UniqueLayout = v);
        _thumbnailPage.SnapToGridChanged += (_, v) => SaveGeneral(g => g.SnapToGrid = v, _streams.ApplyGridSettings);
        _thumbnailPage.GridSizeChanged += (_, v) => SaveGeneral(g => g.GridSize = v, _streams.ApplyGridSettings);

        _hotkeysPage.HotkeysChanged += (_, hk) =>
        {
            _settings.Hotkeys = hk;
            _settingsSvc.SaveSettings();
            _hotkeys.Reload(hk);
            _hotkeysPage.ShowRegistrationResult(_hotkeys.FailedCombos);
        };

        _hotkeysPage.CycleOrderChanged += (_, handles) =>
        {
            _streams.SetCycleOrder(handles);
            _settingsSvc.SaveSettings();
        };

        _zoomPage.ZoomChanged += (_, zoom) =>
        {
            _settings.Zoom = zoom;
            _settingsSvc.SaveSettings();
            _streams.ApplyZoomSettings();
        };

        _languagePage.LanguageSelected += (_, code) =>
        {
            _settings.Language = Loc.Normalize(code);
            Loc.SetLanguage(_settings.Language);
            _settingsSvc.SaveSettings();
        };

        _regionPage.RefreshRequested += (_, __) => RefreshRegionPage();
        _regionPage.DefineRequested += (_, entry) => WithStream(entry?.HWnd, win => _regions.OpenPicker(win));
        _regionPage.NewPresetRequested += (_, entry) => WithStream(entry?.HWnd, win => _regions.OpenPicker(win, newPreset: true));
        _regionPage.AssignRequested += (_, args) => WithStream(args.Stream.HWnd, win =>
        {
            _regions.Assign(win, args.Preset, fit: true);
            RefreshRegionPage();
        });
        _regionPage.DeletePresetRequested += (_, name) => _regions.DeletePreset(name);
    }

    private void LoadPages()
    {
        _thumbnailPage.LoadFrom(_settings.Thumbnail, _settings.General);
        _hotkeysPage.LoadFrom(_settings.Hotkeys);
        _zoomPage.LoadFrom(_settings.Zoom);
    }

    /// <summary>Mutate a General setting, persist it, then optionally react to it.</summary>
    private void SaveGeneral(Action<General> mutate, Action? afterwards = null)
    {
        mutate(_settings.General);
        _settingsSvc.SaveSettings();
        afterwards?.Invoke();
    }

    private void WithStream(IntPtr? hwnd, Action<StreamWindow> action)
    {
        if (hwnd is { } handle && _streams.TryGet(handle, out var win)) action(win);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _layouts.RememberOpenWindows(_streams.Handles);

        if (_settings.General.MinimizeToTray && !_isExplicitExit)
        {
            e.Cancel = true;
            TrayHelper.MinimizeToTray(this);
            return;
        }

        TrayHelper.Ensure(this, false);
        _regions.ClosePicker();
        _streams.CloseAll();
    }

    // ===== Navigation =====

    private void Nav_Thumbnail(object sender, RoutedEventArgs e) => ContentHost.Content = _thumbnailPage;
    private void Nav_Zoom(object sender, RoutedEventArgs e) => ContentHost.Content = _zoomPage;
    private void Nav_Clients(object sender, RoutedEventArgs e) => ContentHost.Content = _clientsPage;
    private void Nav_About(object sender, RoutedEventArgs e) => ContentHost.Content = _aboutPage;

    private void Nav_Hotkeys(object sender, RoutedEventArgs e)
    {
        _hotkeysPage.UpdateOpenThumbnails(
            _streams.Handles
                .Select(h => (Handle: h, Title: WindowEnumerator.GetTitle(h)))
                .Where(p => !string.IsNullOrEmpty(p.Title)));
        ContentHost.Content = _hotkeysPage;
    }

    private void Nav_Region(object sender, RoutedEventArgs e)
    {
        RefreshRegionPage();
        ContentHost.Content = _regionPage;
    }

    private void Nav_Language(object sender, RoutedEventArgs e)
    {
        _languagePage.LoadFrom(Loc.CurrentLanguage);
        ContentHost.Content = _languagePage;
    }

    // ===== Page refreshes =====

    private void RefreshList()
    {
        var self = new WindowInteropHelper(this).Handle;
        _clientsPage.SetWindows(WindowEnumerator.GetTopLevelWindows(self));
    }

    private void RefreshRegionPage()
    {
        _regionPage.SetPresets(_regions.Presets);
        _regionPage.SetStreams(_regions.BuildEntries());
    }

    private void ReopenLastWindows()
    {
        var titles = _layouts.LastOpenWindows();
        if (titles.Count == 0) return;

        var self = new WindowInteropHelper(this).Handle;
        var available = WindowEnumerator.GetTopLevelWindows(self);

        foreach (var title in titles)
        {
            var match = available.FirstOrDefault(w => w.Title == title && !_streams.TryGet(w.HWnd, out _));
            if (match != null) _streams.Open(match);
        }
    }

    // ===== Callbacks from StreamWindow =====

    internal void OnPreviewClicked(IntPtr hwnd) => StreamManager.Focus(hwnd);

    internal void SaveLayoutFor(StreamWindow win) => _layouts.Save(win);

    internal void OpenRegionPickerFor(StreamWindow win) => _regions.OpenPicker(win);
}
