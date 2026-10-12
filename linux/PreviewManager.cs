using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using ClientOPreview.Linux.Platform;
using ClientOPreview.Models;
using ClientOPreview.Services;

namespace ClientOPreview.Linux;

/// <summary>
/// The open previews — the Linux counterpart of Services/StreamManager.cs, plus the bits of
/// LayoutStore and RegionCoordinator the MVP needs. Same rules as on Windows: cycle order kept
/// apart from the dictionary, occurrence index = smallest free one (bug-003), region resolved
/// by window while the preview lives and by layout key across restarts (bug-001).
/// </summary>
internal sealed class PreviewManager
{
    private readonly X11 _x;
    private readonly SettingsService _service;
    private readonly SettingsData _settings;
    private readonly Dictionary<IntPtr, PreviewWindow> _previews = new();
    private readonly List<IntPtr> _order = new();
    private readonly Dictionary<IntPtr, string?> _liveRegion = new();
    private readonly DispatcherTimer _foreground = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _frames = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly CaptureWorker _worker = new(framesPerSecond: 10);
    private readonly Dictionary<IntPtr, CaptureJob> _jobs = new();

    private bool _topmostSuspended;
    private int _cycleIndex = -1;
    private int _stagger;
    private bool _exiting;

    public PreviewManager(X11 x, SettingsService service)
    {
        _x = x;
        _service = service;
        _settings = service.GetSettings();
        _foreground.Tick += (_, __) => CheckForeground();
        _frames.Tick += (_, __) =>
        {
            foreach (var p in _previews.Values) p.RenderFrame();
        };
    }

    /// <summary>The set of open previews (or their order) changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Main window handle, so focusing it does not drop the previews when "only on top while focused".</summary>
    public Func<IntPtr>? OwnMainHandle { get; set; }

    public IEnumerable<PreviewWindow> Windows => _order.Select(h => _previews[h]).ToList();

    public bool IsOpen(IntPtr hwnd) => _previews.ContainsKey(hwnd);

    public void Start()
    {
        _foreground.Start();
        _frames.Start();
    }

    public void Stop()
    {
        _foreground.Stop();
        _frames.Stop();
    }

    // ===== Open / close =====

    public void Open(WindowItem item)
    {
        if (_previews.ContainsKey(item.HWnd)) return;

        var job = _worker.Add(item.HWnd);
        _jobs[item.HWnd] = job;
        var win = new PreviewWindow(item, job)
        {
            Topmost = _settings.General.PreviewsTopmost && !_topmostSuspended,
            OccurrenceIndex = AllocateOccurrence(item.Title)
        };
        var t = _settings.Thumbnail;
        win.SetSize(t.Width, t.Height);
        win.SetTitleFontSize(t.TitleFontSize);
        win.SetHighlightColor(t.ActiveHighlightColor);
        win.ApplyZoomSettings(_settings.Zoom);
        ApplyLayout(win);
        ApplyRegion(win);

        win.Clicked += (_, __) => Focus(item.HWnd);
        win.LayoutSettled += (_, __) => SaveLayout(win);
        win.Opened += (_, __) =>
        {
            _x.SetOpacity(win.OwnHandle, t.OpacityPct / 100.0);
            // The WM may ignore the position asked before mapping and cascade the new window
            // somewhere else (seen with Cinnamon); ask again now that it is mapped.
            if (win.WantedPosition is { } wanted) win.Position = wanted;
            win.EnsureOnScreen();
            win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);
        };
        win.Closed += (_, __) =>
        {
            _previews.Remove(item.HWnd);
            _order.Remove(item.HWnd);
            if (_jobs.Remove(item.HWnd, out var finished)) _worker.Remove(finished);
            _liveRegion.Remove(item.HWnd);
            if (_exiting) return;
            RememberOpen();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        _previews[item.HWnd] = win;
        InsertInCycleOrder(item.HWnd, win);
        win.Show();
        RememberOpen();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close(IntPtr hwnd)
    {
        if (_previews.TryGetValue(hwnd, out var win)) win.Close();
    }

    public void CloseAll()
    {
        foreach (var win in _previews.Values.ToList()) win.Close();
    }

    /// <summary>App exit: close everything but keep the list of open previews for next start.</summary>
    public void Shutdown()
    {
        Stop();
        _exiting = true;
        foreach (var win in _previews.Values.ToList()) win.Close();
        _worker.Dispose();
    }

    /// <summary>Reopens the previews that were open when the app last closed, matched by title.</summary>
    public void RestoreLastOpen()
    {
        var wanted = _service.GetLastOpenWindows();
        if (wanted.Count == 0) return;
        foreach (var item in _x.GetClientWindows(OwnHandles()))
        {
            if (wanted.Contains(item.Title)) Open(item);
        }
    }

    /// <summary>X windows of this app (settings window and previews), never offered as clients.</summary>
    public HashSet<IntPtr> OwnHandles()
    {
        var own = _previews.Values.Select(p => p.OwnHandle).ToHashSet();
        if (OwnMainHandle?.Invoke() is { } main && main != IntPtr.Zero) own.Add(main);
        return own;
    }

    private void RememberOpen()
    {
        var titles = _order.Select(_x.GetTitle).Where(s => s.Length > 0).Distinct().ToList();
        _service.SetLastOpenWindows(titles);
    }

    private int AllocateOccurrence(string title)
    {
        var used = _previews.Values.Where(w => w.WindowTitle == title).Select(w => w.OccurrenceIndex).ToHashSet();
        int index = 0;
        while (used.Contains(index)) index++;
        return index;
    }

    // ===== Layout =====

    private string LayoutKeyOf(PreviewWindow win)
    {
        if (!_settings.General.TrackLocations) return string.Empty;
        if (!_settings.General.UniqueLayout) return LayoutKey.Shared;
        return LayoutKey.For(win.WindowTitle, win.OccurrenceIndex);
    }

    private void ApplyLayout(PreviewWindow win)
    {
        var key = LayoutKeyOf(win);
        if (key.Length > 0 && LayoutKey.TryParseGeometry(_service.GetLayout(key), out var l, out var t, out var w, out var h))
        {
            win.WantedPosition = new PixelPoint(l, t);
            win.Position = new PixelPoint(l, t);
            win.Width = Math.Max(win.MinWidth, w);
            win.Height = Math.Max(win.MinHeight, h);
            return;
        }
        // Stagger inside the primary monitor: (0,0) of the desktop may belong to no monitor at all.
        var area = win.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 800, 600);
        win.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.Manual;
        var start = new PixelPoint(area.X + 50 + (_stagger % 10) * 30, area.Y + 50 + (_stagger % 10) * 30);
        win.WantedPosition = start;
        win.Position = start;
        _stagger++;
    }

    private void SaveLayout(PreviewWindow win)
    {
        var key = LayoutKeyOf(win);
        if (key.Length == 0) return;
        _service.SetLayout(key, LayoutKey.FormatGeometry(win.Position.X, win.Position.Y, win.Width, win.Height));
    }

    // ===== Regions =====

    public string RegionKeyOf(PreviewWindow win)
    {
        var title = _x.GetTitle(win.SourceHwnd);
        if (title.Length == 0) title = win.WindowTitle;
        return LayoutKey.For(title, win.OccurrenceIndex);
    }

    public string? ResolveRegion(PreviewWindow win)
    {
        if (_liveRegion.TryGetValue(win.SourceHwnd, out var live)) return live;
        return _settings.Regions.Assignments.TryGetValue(RegionKeyOf(win), out var name) ? name : null;
    }

    public void ApplyRegion(PreviewWindow win) =>
        win.ApplyRegion(_settings.Regions.Enabled ? _settings.Regions.FindPreset(ResolveRegion(win)) : null);

    /// <summary>Master switch for every preview; assignments stay untouched.</summary>
    public void SetRegionsEnabled(bool enabled)
    {
        if (_settings.Regions.Enabled == enabled) return;
        _settings.Regions.Enabled = enabled;
        _service.SaveSettings();
        foreach (var win in _previews.Values) ApplyRegion(win);
    }

    public void AssignRegion(PreviewWindow win, string? preset, bool fit)
    {
        _liveRegion[win.SourceHwnd] = string.IsNullOrEmpty(preset) ? null : preset;
        var key = RegionKeyOf(win);
        if (string.IsNullOrEmpty(preset)) _settings.Regions.Assignments.Remove(key);
        else _settings.Regions.Assignments[key] = preset;
        _service.SaveSettings();
        ApplyRegion(win);
        if (fit) win.FitToRegion();
    }

    public void DeletePreset(string name)
    {
        _settings.Regions.RemovePreset(name);
        foreach (var hwnd in _liveRegion.Keys.ToList())
        {
            if (string.Equals(_liveRegion[hwnd], name, StringComparison.OrdinalIgnoreCase)) _liveRegion[hwnd] = null;
        }
        _service.SaveSettings();
        foreach (var win in _previews.Values) ApplyRegion(win);
    }

    // ===== Settings =====

    public void ApplyThumbnailSettings()
    {
        var t = _settings.Thumbnail;
        foreach (var win in _previews.Values)
        {
            win.SetSize(t.Width, t.Height);
            win.SetTitleFontSize(t.TitleFontSize);
            win.SetHighlightColor(t.ActiveHighlightColor);
            _x.SetOpacity(win.OwnHandle, Math.Clamp(t.OpacityPct / 100.0, 0.2, 1.0));
        }
    }

    public void ApplyGeneralSettings()
    {
        foreach (var win in _previews.Values)
            win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);
        _topmostSuspended = false;
        ApplyTopmost();
    }

    private void ApplyTopmost()
    {
        var topmost = _settings.General.PreviewsTopmost && !_topmostSuspended;
        foreach (var win in _previews.Values) win.Topmost = topmost;
    }

    // ===== Cycle order and focus =====

    public void MoveInCycle(IntPtr hwnd, int delta)
    {
        int i = _order.IndexOf(hwnd);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= _order.Count) return;

        var focused = _cycleIndex >= 0 && _cycleIndex < _order.Count ? _order[_cycleIndex] : IntPtr.Zero;
        (_order[i], _order[j]) = (_order[j], _order[i]);
        _cycleIndex = focused == IntPtr.Zero ? -1 : _order.IndexOf(focused);

        _settings.Hotkeys.CycleOrder = _order.Select(h => LayoutKey.For(_previews[h].WindowTitle, _previews[h].OccurrenceIndex)).ToList();
        _service.SaveSettings();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void InsertInCycleOrder(IntPtr hwnd, PreviewWindow win)
    {
        int rank = RankOf(LayoutKey.For(win.WindowTitle, win.OccurrenceIndex));
        int at = _order.Count;
        for (int i = 0; i < _order.Count; i++)
        {
            var other = _previews[_order[i]];
            if (RankOf(LayoutKey.For(other.WindowTitle, other.OccurrenceIndex)) > rank) { at = i; break; }
        }
        _order.Insert(at, hwnd);
    }

    private int RankOf(string key)
    {
        var rank = _settings.Hotkeys.CycleOrder.IndexOf(key);
        return rank < 0 ? int.MaxValue : rank;
    }

    public void Focus(IntPtr hwnd)
    {
        _x.Activate(hwnd);
        int i = _order.IndexOf(hwnd);
        if (i >= 0) _cycleIndex = i;
    }

    public void CycleNext()
    {
        if (_order.Count == 0) return;
        _cycleIndex = (_cycleIndex + 1) % _order.Count;
        _x.Activate(_order[_cycleIndex]);
    }

    public void ActivateByIndex(int index)
    {
        if (_order.Count == 0) return;

        // A hotkey mapped to a specific client wins over the positional order.
        if (_settings.Hotkeys.DirectKeyMappings.TryGetValue(index, out var title) && !string.IsNullOrEmpty(title))
        {
            foreach (var hwnd in _order)
            {
                if (_x.GetTitle(hwnd) == title)
                {
                    Focus(hwnd);
                    return;
                }
            }
        }

        if (index >= 0 && index < _order.Count) Focus(_order[index]);
    }

    private void CheckForeground()
    {
        try
        {
            foreach (var hwnd in _previews.Keys.ToList())
            {
                // Reported by the capture thread, so the poll costs no X round trip per preview.
                if (_jobs.TryGetValue(hwnd, out var job) && job.Gone) _previews[hwnd].Close();
            }

            var active = _x.GetActiveWindow();
            foreach (var kv in _previews) kv.Value.SetActiveState(kv.Key == active);

            if (_settings.General.PreviewsTopmost && _settings.General.TopmostOnlyWhenClientFocused)
            {
                bool own = active != IntPtr.Zero
                    && (active == OwnMainHandle?.Invoke() || _previews.Values.Any(p => p.OwnHandle == active));
                bool suspend = !(_previews.ContainsKey(active) || own);
                if (suspend != _topmostSuspended)
                {
                    _topmostSuspended = suspend;
                    ApplyTopmost();
                }
            }
            else if (_topmostSuspended)
            {
                _topmostSuspended = false;
                ApplyTopmost();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("foreground poll failed", ex);
        }
    }
}
