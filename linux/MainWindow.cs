using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClientOPreview.Linux.Platform;
using ClientOPreview.Localization;
using ClientOPreview.Models;
using ClientOPreview.Services;

namespace ClientOPreview.Linux;

/// <summary>
/// Settings window of the Linux build, built in code (one file instead of a XAML pair per tab).
/// Same flow as the Windows MainWindow: a tab edits the model, then the manager applies it and
/// settings.json is saved. Changing language rebuilds the tabs.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly X11 _x;
    private readonly SettingsService _service;
    private readonly SettingsData _settings;
    private readonly PreviewManager _previews;
    private readonly X11Hotkeys? _hotkeys;
    private IReadOnlyList<string> _failedHotkeys = Array.Empty<string>();

    private TabControl _tabs = new();
    private List<WindowItem> _clients = new();
    private ListBox _clientList = new();
    private ListBox _cycleList = new();
    private ComboBox _regionPreviews = new();
    private ComboBox _regionPresets = new();
    private TextBlock _hotkeyWarning = new();

    public MainWindow(X11 x, SettingsService service)
    {
        _x = x;
        _service = service;
        _settings = service.GetSettings();
        _previews = new PreviewManager(x, service) { OwnMainHandle = () => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero };
        _previews.Changed += (_, __) => RefreshOpenLists();

        Title = "client-o-preview";
        Width = 760;
        Height = 560;

        try
        {
            _hotkeys = new X11Hotkeys();
            _hotkeys.CycleRequested += (_, __) => Dispatcher.UIThread.Post(_previews.CycleNext);
            _hotkeys.DirectRequested += (_, i) => Dispatcher.UIThread.Post(() => _previews.ActivateByIndex(i));
            _hotkeys.Registered += (_, failed) => Dispatcher.UIThread.Post(() =>
            {
                _failedHotkeys = failed;
                UpdateHotkeyWarning();
            });
            _hotkeys.Reload(_settings.Hotkeys);
        }
        catch (Exception ex)
        {
            AppLog.Error("hotkeys unavailable", ex);
        }

        BuildUi();
        Loc.LanguageChanged += (_, __) => BuildUi();

        Opened += (_, __) =>
        {
            _previews.Start();
            _previews.RestoreLastOpen();
            RefreshClients();
        };
        Closing += (_, __) =>
        {
            _previews.Shutdown();
            _hotkeys?.Dispose();
        };
    }

    private void BuildUi()
    {
        int selected = _tabs.SelectedIndex;
        _tabs = new TabControl { TabStripPlacement = Dock.Left, Padding = new Thickness(12) };
        _tabs.Items.Add(Tab("NavClients", BuildClientsTab()));
        _tabs.Items.Add(Tab("NavThumbnail", BuildThumbnailTab()));
        _tabs.Items.Add(Tab("NavRegion", BuildRegionTab()));
        _tabs.Items.Add(Tab("NavHotkeys", BuildHotkeysTab()));
        _tabs.Items.Add(Tab("NavLanguage", BuildLanguageTab()));
        _tabs.Items.Add(Tab("NavAbout", BuildAboutTab()));
        _tabs.SelectedIndex = Math.Max(0, selected);
        Content = _tabs;
        RefreshClients();
        RefreshOpenLists();
        UpdateHotkeyWarning();
    }

    // ===== Small builders =====

    private static string T(string key) => Loc.Get(key);

    private static TabItem Tab(string key, Control content) => new()
    {
        Header = T(key),
        FontSize = 14,
        Content = new ScrollViewer { Content = content }
    };

    private static TextBlock Header(string key) => new() { Text = T(key), FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) };

    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    private static TextBlock Hint(string text) => new() { Text = text, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    private static Button Btn(string key, Action click)
    {
        var b = new Button { Content = T(key) };
        b.Click += (_, __) => click();
        return b;
    }

    private static StackPanel Row(params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    private static StackPanel Column(params Control[] children)
    {
        var p = new StackPanel { Spacing = 8 };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    private static NumericUpDown Number(double value, double min, double max, double step = 1) => new()
    {
        Value = (decimal)value, Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)step,
        Width = 140, FormatString = step < 1 ? "0.0" : "0"
    };

    private static CheckBox Check(string key, bool value) => new() { Content = T(key), IsChecked = value };

    // ===== Clients =====

    private Control BuildClientsTab()
    {
        _clientList = new ListBox { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle, MinHeight = 300 };
        return Column(
            Header("NavClients"),
            Label(T("ClientsSelectWindows")),
            _clientList,
            Row(Btn("ClientsRefresh", RefreshClients),
                Btn("ClientsOpenStreams", OpenSelected),
                Btn("ClientsCloseSelected", CloseSelected),
                Btn("ClientsCloseAll", _previews.CloseAll)));
    }

    private void RefreshClients()
    {
        _clients = _x.GetClientWindows(_previews.OwnHandles());
        _clientList.ItemsSource = _clients
            .Select(c => (_previews.IsOpen(c.HWnd) ? "● " : "   ") + c.Display)
            .ToList();
    }

    private IEnumerable<WindowItem> SelectedClients() =>
        _clientList.Selection.SelectedIndexes.Where(i => i >= 0 && i < _clients.Count).Select(i => _clients[i]).ToList();

    private void OpenSelected()
    {
        foreach (var c in SelectedClients()) _previews.Open(c);
        RefreshClients();
    }

    private void CloseSelected()
    {
        foreach (var c in SelectedClients()) _previews.Close(c.HWnd);
        RefreshClients();
    }

    // ===== Thumbnail =====

    private Control BuildThumbnailTab()
    {
        var t = _settings.Thumbnail;
        var g = _settings.General;

        var width = Number(t.Width, 80, 1920);
        var height = Number(t.Height, 40, 1080);
        var opacity = Number(t.OpacityPct, 20, 100);
        var font = Number(t.TitleFontSize, 8, 20);
        var color = new TextBox { Text = t.ActiveHighlightColor, Width = 140 };

        var topmost = Check("ThumbTopmost", g.PreviewsTopmost);
        var onlyFocused = Check("ThumbTopmostOnlyFocused", g.TopmostOnlyWhenClientFocused);
        var track = Check("ThumbTrackLocations", g.TrackLocations);
        var unique = Check("ThumbUniqueLayout", g.UniqueLayout);
        var snap = Check("ThumbSnapToGrid", g.SnapToGrid);
        var grid = Number(g.GridSize, 2, 200);

        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto") };
        void Put(int row, string key, Control c)
        {
            var l = Label(T(key));
            l.Margin = new Thickness(0, 3, 12, 3);
            Grid.SetRow(l, row);
            Grid.SetRow(c, row);
            Grid.SetColumn(c, 1);
            form.Children.Add(l);
            form.Children.Add(c);
        }
        Put(0, "ThumbWidth", width);
        Put(1, "ThumbHeight", height);
        Put(2, "ThumbOpacity", opacity);
        Put(3, "ThumbTitleSize", font);
        Put(4, "ThumbActiveColor", color);

        void Apply()
        {
            t.Width = (int)(width.Value ?? t.Width);
            t.Height = (int)(height.Value ?? t.Height);
            t.OpacityPct = (int)(opacity.Value ?? t.OpacityPct);
            t.TitleFontSize = (int)(font.Value ?? t.TitleFontSize);
            if (Color.TryParse(color.Text, out _)) t.ActiveHighlightColor = color.Text!.Trim();

            g.PreviewsTopmost = topmost.IsChecked == true;
            g.TopmostOnlyWhenClientFocused = onlyFocused.IsChecked == true;
            g.TrackLocations = track.IsChecked == true;
            g.UniqueLayout = unique.IsChecked == true;
            g.SnapToGrid = snap.IsChecked == true;
            g.GridSize = (int)(grid.Value ?? g.GridSize);

            _previews.ApplyThumbnailSettings();
            _previews.ApplyGeneralSettings();
            _service.SaveSettings();
        }

        return Column(
            Header("ThumbTitle"),
            form,
            new Separator(),
            Label(T("ThumbPreviewsSection")),
            topmost, onlyFocused, Hint(T("ThumbTopmostOnlyFocusedHint")),
            new Separator(),
            Label(T("ThumbGeneralSection")),
            track, unique, Row(snap, Label(T("ThumbGridSize")), grid),
            Btn("ThumbApply", Apply));
    }

    // ===== Region =====

    private Control BuildRegionTab()
    {
        _regionPreviews = new ComboBox { MinWidth = 320 };
        _regionPresets = new ComboBox { MinWidth = 320 };

        var name = new TextBox { Watermark = T("PickerPresetName"), Width = 220 };
        var left = Number(0, 0, 99, 0.5);
        var top = Number(0, 0, 99, 0.5);
        var w = Number(100, 1, 100, 0.5);
        var h = Number(100, 1, 100, 0.5);
        var lockAspect = Check("PickerLockAspect", true);
        var fit = Check("PickerFitWindow", false);
        var message = new TextBlock { Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap };

        PreviewWindow? SelectedPreview() =>
            _regionPreviews.SelectedIndex >= 0 ? _previews.Windows.ElementAtOrDefault(_regionPreviews.SelectedIndex) : null;

        string? SelectedPreset() =>
            _regionPresets.SelectedIndex > 0 ? _settings.Regions.Presets[_regionPresets.SelectedIndex - 1].Name : null;

        // Picking a preset fills the editor, so a variant is one edit + "save as new" away.
        _regionPresets.SelectionChanged += (_, __) =>
        {
            var p = _settings.Regions.FindPreset(SelectedPreset());
            if (p == null) return;
            left.Value = (decimal)(p.X * 100);
            top.Value = (decimal)(p.Y * 100);
            w.Value = (decimal)(p.W * 100);
            h.Value = (decimal)(p.H * 100);
            lockAspect.IsChecked = p.LockAspect;
        };

        void SaveNew()
        {
            message.Text = "";
            var n = name.Text?.Trim() ?? "";
            if (n.Length == 0) { message.Text = T("PickerNameRequired"); return; }
            // Saved presets are never overwritten, same rule as the Windows picker.
            if (_settings.Regions.FindPreset(n) != null) { message.Text = Loc.Format("RegionNameTaken", n); return; }

            double x = (double)(left.Value ?? 0) / 100, y = (double)(top.Value ?? 0) / 100;
            var preset = new RegionPreset
            {
                Name = n,
                X = x,
                Y = y,
                W = Math.Min(1 - x, (double)(w.Value ?? 100) / 100),
                H = Math.Min(1 - y, (double)(h.Value ?? 100) / 100),
                LockAspect = lockAspect.IsChecked == true
            };
            _settings.Regions.UpsertPreset(preset);
            _service.SaveSettings();
            var win = SelectedPreview();
            if (win != null) _previews.AssignRegion(win, n, fit.IsChecked == true);
            RefreshOpenLists(selectPreset: n);
        }

        void ApplyPreset()
        {
            var win = SelectedPreview();
            if (win == null) { message.Text = T("RegionSelectFirst"); return; }
            _previews.AssignRegion(win, SelectedPreset(), fit.IsChecked == true);
        }

        void Clear()
        {
            var win = SelectedPreview();
            if (win == null) { message.Text = T("RegionSelectFirst"); return; }
            _previews.AssignRegion(win, null, false);
            RefreshOpenLists();
        }

        void Delete()
        {
            var p = SelectedPreset();
            if (p == null) return;
            _previews.DeletePreset(p);
            RefreshOpenLists();
        }

        return Column(
            Header("RegionTitle"),
            Hint(T("RegionDesc")),
            Label(T("RegionOpenPreviews")), _regionPreviews,
            Label(T("RegionSavedPresets")), Row(_regionPresets, Btn("RegionDeletePreset", Delete)),
            Row(Btn("LinuxRegionApply", ApplyPreset), Btn("RegionClear", Clear)),
            new Separator(),
            Label(T("LinuxRegionEditor")), Hint(T("LinuxRegionHint")),
            name,
            Row(Label(T("LinuxRegionLeft")), left, Label(T("LinuxRegionTop")), top),
            Row(Label(T("LinuxRegionWidth")), w, Label(T("LinuxRegionHeight")), h),
            lockAspect, fit,
            Btn("LinuxRegionSaveNew", SaveNew),
            message,
            Hint(T("RegionFooter")));
    }

    // ===== Hotkeys =====

    private Control BuildHotkeysTab()
    {
        var hk = _settings.Hotkeys;
        var enabled = Check("HotkeysEnable", hk.Enabled);

        (CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win) Mods(string? mods)
        {
            var m = KeyNames.ModifiersOf(mods);
            return (new CheckBox { Content = "Ctrl", IsChecked = (m & KeyNames.Control) != 0 },
                    new CheckBox { Content = "Alt", IsChecked = (m & KeyNames.Alt) != 0 },
                    new CheckBox { Content = "Shift", IsChecked = (m & KeyNames.Shift) != 0 },
                    new CheckBox { Content = "Super", IsChecked = (m & KeyNames.Super) != 0 });
        }
        static string ModsText((CheckBox c, CheckBox a, CheckBox s, CheckBox w) m) =>
            KeyNames.FormatModifiers(m.c.IsChecked == true, m.a.IsChecked == true, m.s.IsChecked == true, m.w.IsChecked == true);

        var cycleMods = Mods(hk.CycleModifiers);
        var cycleKey = KeyBox(hk.CycleKey);
        var directMods = Mods(hk.DirectModifiers);
        var directKeys = Enumerable.Range(0, X11Hotkeys.DirectCount)
            .Select(i => KeyBox(i < hk.DirectKeys.Count ? hk.DirectKeys[i] : ""))
            .ToList();

        var directGrid = new WrapPanel();
        for (int i = 0; i < directKeys.Count; i++)
            directGrid.Children.Add(Row(Label(Loc.Format("HotkeysItemLabel", i + 1)), directKeys[i]));

        _hotkeyWarning = new TextBlock { Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap };

        void Apply()
        {
            hk.Enabled = enabled.IsChecked == true;
            hk.CycleModifiers = ModsText(cycleMods);
            hk.CycleKey = cycleKey.Text ?? "";
            hk.DirectModifiers = ModsText(directMods);
            hk.DirectKeys = directKeys.Select(b => b.Text ?? "").ToList();
            _service.SaveSettings();
            _hotkeys?.Reload(hk);
        }

        _cycleList = new ListBox { MinHeight = 120 };
        void Move(int delta)
        {
            var win = _previews.Windows.ElementAtOrDefault(_cycleList.SelectedIndex);
            if (win == null) return;
            _previews.MoveInCycle(win.SourceHwnd, delta);
            _cycleList.SelectedIndex = Math.Clamp(_cycleList.SelectedIndex + delta, 0, _previews.Windows.Count() - 1);
        }

        return Column(
            Header("HotkeysTitle"),
            enabled,
            _hotkeyWarning,
            Label(T("HotkeysCycleTitle")), Hint(T("HotkeysCycleDesc")),
            Row(Label(T("HotkeysModifiers")), cycleMods.ctrl, cycleMods.alt, cycleMods.shift, cycleMods.win),
            Row(Label(T("HotkeysKey")), cycleKey, Hint(T("HotkeysKeyHint"))),
            new Separator(),
            Label(T("HotkeysDirectTitle")), Hint(T("HotkeysDirectDesc")),
            Row(Label(T("HotkeysModifiers")), directMods.ctrl, directMods.alt, directMods.shift, directMods.win),
            directGrid,
            Btn("ThumbApply", Apply),
            new Separator(),
            Label(T("LinuxCycleOrder")),
            _cycleList,
            Row(Btn("LinuxMoveUp", () => Move(-1)), Btn("LinuxMoveDown", () => Move(1))));
    }

    /// <summary>Read-only box that records the next key pressed; Esc, Delete or Backspace clear it.</summary>
    private static TextBox KeyBox(string value)
    {
        var box = new TextBox { Text = value, IsReadOnly = true, Width = 110 };
        ToolTip.SetTip(box, T("HotkeysKeyTooltip"));
        box.AddHandler(KeyDownEvent, (_, e) =>
        {
            e.Handled = true; // Tab included: it is a valid hotkey here, not focus navigation
            switch (e.Key)
            {
                case Key.Escape or Key.Delete or Key.Back:
                    box.Text = "";
                    return;
                case Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
                    or Key.RightShift or Key.LWin or Key.RWin or Key.None:
                    return;
            }
            // Avalonia's Key enum mirrors WPF's, so the stored name is the same on both OSes.
            box.Text = e.Key.ToString();
        }, RoutingStrategies.Tunnel);
        return box;
    }

    private void UpdateHotkeyWarning()
    {
        _hotkeyWarning.Text = _failedHotkeys.Count == 0 ? "" : Loc.Format("LinuxHotkeysFailed", string.Join(", ", _failedHotkeys));
        _hotkeyWarning.IsVisible = _failedHotkeys.Count > 0;
    }

    // ===== Language / About =====

    private Control BuildLanguageTab()
    {
        var pt = new RadioButton { GroupName = "lang", Content = "Português (Brasil)", IsChecked = Loc.CurrentLanguage == Loc.PortugueseBr };
        var en = new RadioButton { GroupName = "lang", Content = "English", IsChecked = Loc.CurrentLanguage == Loc.English };
        void Set(string code)
        {
            if (Loc.CurrentLanguage == code) return;
            _settings.Language = code;
            _service.SaveSettings();
            // Rebuilds the tabs (LanguageChanged) — deferred so the radio's own event finishes first.
            Dispatcher.UIThread.Post(() => Loc.SetLanguage(code));
        }
        pt.IsCheckedChanged += (_, __) => { if (pt.IsChecked == true) Set(Loc.PortugueseBr); };
        en.IsCheckedChanged += (_, __) => { if (en.IsChecked == true) Set(Loc.English); };
        return Column(Header("LanguageTitle"), Hint(T("LanguageDesc")), pt, en);
    }

    private static Control BuildAboutTab()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return Column(
            Header("AboutTitle"),
            Label($"client-o-preview {version?.ToString(3)} (Linux)"),
            Hint(T("AboutDisclaimer")),
            Hint(T("LinuxAboutNote")),
            Hint($"{AppLog.Directory}"));
    }

    // ===== Lists that follow the open previews =====

    private void RefreshOpenLists() => RefreshOpenLists(null);

    private void RefreshOpenLists(string? selectPreset)
    {
        var wins = _previews.Windows.ToList();
        string Name(PreviewWindow w) => w.OccurrenceIndex > 0 ? $"{w.WindowTitle}  #{w.OccurrenceIndex + 1}" : w.WindowTitle;

        int cycleSel = _cycleList.SelectedIndex;
        _cycleList.ItemsSource = wins.Select((w, i) => $"{i + 1}. {Name(w)}").ToList();
        _cycleList.SelectedIndex = Math.Min(cycleSel, wins.Count - 1);

        int prevSel = _regionPreviews.SelectedIndex;
        _regionPreviews.ItemsSource = wins.Select(w => $"{Name(w)}  [{_previews.ResolveRegion(w) ?? "—"}]").ToList();
        _regionPreviews.SelectedIndex = wins.Count == 0 ? -1 : Math.Clamp(prevSel, 0, wins.Count - 1);

        var presetNames = new List<string> { T("RegionNone") };
        presetNames.AddRange(_settings.Regions.Presets.Select(p => p.Name));
        var current = selectPreset ?? (_regionPresets.SelectedItem as string);
        _regionPresets.ItemsSource = presetNames;
        int idx = current == null ? 0 : presetNames.IndexOf(current);
        _regionPresets.SelectedIndex = Math.Max(0, idx);

        RefreshClients();
    }
}
