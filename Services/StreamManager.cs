using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ClientOPreview.Models;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Services;

/// <summary>
/// Owns the open previews: one <see cref="StreamWindow"/> per monitored client.
/// Also runs the foreground poll that highlights the active client, reaps previews whose
/// source window died, and drives the "only on top while a client is focused" behaviour.
/// </summary>
public sealed class StreamManager
{
    private readonly MainWindow _owner;
    private readonly SettingsData _settings;
    private readonly Dictionary<IntPtr, StreamWindow> _streams = new();

    // Cycle order, kept apart from _streams because the user can rearrange it and a dictionary
    // has no order to promise. Every handle in here is a live key of _streams, and vice versa.
    private readonly List<IntPtr> _order = new();

    private readonly DispatcherTimer _foreground = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private bool _topmostSuspended;
    private int _cycleIndex = -1;

    public StreamManager(MainWindow owner, SettingsData settings)
    {
        _owner = owner;
        _settings = settings;
        _foreground.Tick += (_, __) => CheckForeground();
    }

    /// <summary>Raised after the preview is built and before it is shown.</summary>
    public event EventHandler<StreamWindow>? Opening;

    public event EventHandler<IntPtr>? StreamClosed;

    public int Count => _streams.Count;

    /// <summary>Live previews in cycle order.</summary>
    public IEnumerable<IntPtr> Handles => _order.ToList();

    public IEnumerable<StreamWindow> Windows => _streams.Values.ToList();

    public bool TryGet(IntPtr hwnd, out StreamWindow win) => _streams.TryGetValue(hwnd, out win!);

    public void Start() => _foreground.Start();

    public void Stop() => _foreground.Stop();

    // ===== Opening / closing =====

    public void Open(WindowItem item)
    {
        if (_streams.TryGetValue(item.HWnd, out var existing))
        {
            existing.Activate();
            return;
        }

        var win = new StreamWindow(_owner, item)
        {
            Topmost = _settings.General.PreviewsTopmost && !_topmostSuspended,
            OccurrenceIndex = AllocateOccurrence(item.Title)
        };

        var t = _settings.Thumbnail;
        win.SetSize(t.Width, t.Height);
        win.SetOpacity(t.OpacityPct / 100.0);
        win.SetTitleFontSize(t.TitleFontSize);
        win.SetHighlightColor(t.ActiveHighlightColor);
        win.ApplyZoomSettings(_settings.Zoom);
        win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);

        win.Closed += (_, __) =>
        {
            _streams.Remove(item.HWnd);
            _order.Remove(item.HWnd);
            StreamClosed?.Invoke(this, item.HWnd);
        };
        _streams[item.HWnd] = win;
        InsertInCycleOrder(item.HWnd, win);

        // Layout and region both key off the occurrence index, so the stream has to be
        // registered before anyone gets a say.
        Opening?.Invoke(this, win);
        win.Show();
    }

    public void Close(IEnumerable<WindowItem> items)
    {
        foreach (var item in items.ToList())
        {
            if (_streams.TryGetValue(item.HWnd, out var win)) win.Close();
        }
    }

    public void CloseAll()
    {
        foreach (var win in _streams.Values.ToList()) win.Close();
        _streams.Clear();
        _order.Clear();
    }

    // ===== Cycle order =====

    /// <summary>
    /// Rearranges the cycle, in the order the user dragged the list into. Handles that are no
    /// longer open are dropped and open previews the caller forgot are kept at the end, so a
    /// stale list can never make a preview unreachable. The order is remembered per preview
    /// (same key as the saved layout), so it survives a restart.
    /// </summary>
    public void SetCycleOrder(IEnumerable<IntPtr> handles)
    {
        var current = _order.ToList();
        var focused = _cycleIndex >= 0 && _cycleIndex < current.Count ? current[_cycleIndex] : IntPtr.Zero;

        var reordered = handles.Where(h => _streams.ContainsKey(h)).Distinct().ToList();
        foreach (var hwnd in current)
        {
            if (!reordered.Contains(hwnd)) reordered.Add(hwnd);
        }

        _order.Clear();
        _order.AddRange(reordered);

        // Keep pointing at the same client, so the next press continues from where it was.
        _cycleIndex = focused == IntPtr.Zero ? -1 : _order.IndexOf(focused);

        _settings.Hotkeys.CycleOrder = _order.Select(CycleKeyOf).Where(k => k.Length > 0).ToList();
    }

    /// <summary>Places a fresh preview where the saved order says it belongs, or last when it is new.</summary>
    private void InsertInCycleOrder(IntPtr hwnd, StreamWindow win)
    {
        var rank = SavedRankOf(LayoutKey.For(win.WindowTitle, win.OccurrenceIndex));
        int at = _order.Count;
        for (int i = 0; i < _order.Count; i++)
        {
            if (SavedRankOf(CycleKeyOf(_order[i])) > rank) { at = i; break; }
        }
        _order.Insert(at, hwnd);
    }

    private int SavedRankOf(string key)
    {
        var saved = _settings.Hotkeys.CycleOrder;
        var rank = key.Length == 0 ? -1 : saved.IndexOf(key);
        return rank < 0 ? int.MaxValue : rank;
    }

    private string CycleKeyOf(IntPtr hwnd) =>
        _streams.TryGetValue(hwnd, out var win) ? LayoutKey.For(win.WindowTitle, win.OccurrenceIndex) : "";

    /// <summary>
    /// Smallest index not in use by another live preview with the same title. Reusing the
    /// count would hand a fresh preview an index that a still-open one already owns, and the
    /// two would then fight over the same layout/region key (bug-003).
    /// </summary>
    private int AllocateOccurrence(string title)
    {
        var used = new HashSet<int>();
        foreach (var win in _streams.Values)
        {
            if (string.Equals(win.WindowTitle, title, StringComparison.Ordinal)) used.Add(win.OccurrenceIndex);
        }

        int index = 0;
        while (used.Contains(index)) index++;
        return index;
    }

    // ===== Applying settings =====

    public void ApplyThumbnailSettings()
    {
        var t = _settings.Thumbnail;
        var alpha = Math.Max(0.2, Math.Min(1.0, t.OpacityPct / 100.0));
        foreach (var win in _streams.Values)
        {
            win.SetOpacity(alpha);
            win.SetSize(t.Width, t.Height);
            win.SetTitleFontSize(t.TitleFontSize);
            win.SetHighlightColor(t.ActiveHighlightColor);
        }
    }

    public void ApplyZoomSettings()
    {
        foreach (var win in _streams.Values) win.ApplyZoomSettings(_settings.Zoom);
    }

    public void ApplyGridSettings()
    {
        foreach (var win in _streams.Values)
            win.ApplyGridSettings(_settings.General.SnapToGrid, _settings.General.GridSize);
    }

    public void ApplyTopmost()
    {
        var topmost = _settings.General.PreviewsTopmost && !_topmostSuspended;
        foreach (var win in _streams.Values) win.Topmost = topmost;
    }

    /// <summary>Call when the user changes a topmost option, so previews come back up at once.</summary>
    public void ResumeTopmost()
    {
        _topmostSuspended = false;
        ApplyTopmost();
    }

    // ===== Focus =====

    /// <summary>
    /// Brings a client window to the front. Windows refuses <c>SetForegroundWindow</c> from a
    /// process that is not itself in the foreground, which is exactly the case when a global
    /// hotkey fires while the game has focus. So: try once, and if the window did not actually
    /// come up, attach our input queue to the current foreground thread — for the length of that
    /// attachment the OS treats both threads as one, and the call is allowed.
    /// </summary>
    public static void Focus(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        if (TryForeground(hwnd)) return;

        var fg = GetForegroundWindow();
        uint ourThread = GetCurrentThreadId();
        uint fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);

        if (fgThread != 0 && fgThread != ourThread)
        {
            if (AttachThreadInput(ourThread, fgThread, true))
            {
                try
                {
                    BringWindowToTop(hwnd);
                    if (TryForeground(hwnd)) return;
                }
                finally
                {
                    AttachThreadInput(ourThread, fgThread, false);
                }
            }
            else
            {
                // Error 5 means the foreground window runs at a higher integrity level than we do
                // (client started as administrator): UIPI blocks the attach, and no API gets around it.
                var err = Marshal.GetLastWin32Error();
                AppLog.Warn("Focus", err == 5
                    ? "AttachThreadInput denied (win32 5): the focused window runs elevated and this app does not — run the app as administrator"
                    : $"AttachThreadInput failed (win32 {err})");
            }
        }

        // Last resort: the shell's own activation path, which plays by slightly different rules.
        SwitchToThisWindow(hwnd, true);
        if (GetForegroundWindow() == hwnd) return;

        AppLog.Warn("Focus", $"could not activate 0x{hwnd.ToInt64():X} '{WindowEnumerator.GetTitle(hwnd)}'; " +
                             $"foreground stayed 0x{GetForegroundWindow().ToInt64():X} '{WindowEnumerator.GetTitle(GetForegroundWindow())}'");
    }

    private static bool TryForeground(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        return GetForegroundWindow() == hwnd;
    }

    public void CycleNext()
    {
        var handles = _order.ToList();
        if (handles.Count == 0) return;

        _cycleIndex = (_cycleIndex + 1) % handles.Count;
        Focus(handles[_cycleIndex]);
    }

    public void ActivateByIndex(int index)
    {
        if (_streams.Count == 0) return;

        // A hotkey mapped to a specific client wins over the positional order.
        if (_settings.Hotkeys.DirectKeyMappings.TryGetValue(index, out var title) && !string.IsNullOrEmpty(title))
        {
            foreach (var hwnd in _order)
            {
                if (WindowEnumerator.GetTitle(hwnd) == title)
                {
                    Focus(hwnd);
                    return;
                }
            }
        }

        var handles = _order.ToList();
        if (index < 0 || index >= handles.Count) return;
        Focus(handles[index]);
        _cycleIndex = index;
    }

    public void CheckForeground()
    {
        try
        {
            foreach (var hwnd in _streams.Keys.ToList())
            {
                if (!IsWindow(hwnd) && _streams.TryGetValue(hwnd, out var dead)) dead.Close();
            }

            var fg = GetForegroundWindow();
            foreach (var kv in _streams) kv.Value.SetActiveState(kv.Key == fg);

            UpdateTopmostForFocus(fg);
        }
        catch (Exception ex)
        {
            AppLog.Warn("foreground poll failed", ex);
        }
    }

    /// <summary>
    /// With "only on top while a client is in focus", previews sink behind other windows as
    /// soon as the user alt-tabs to something that is neither a client nor part of this app.
    /// </summary>
    private void UpdateTopmostForFocus(IntPtr fg)
    {
        if (!_settings.General.PreviewsTopmost)
        {
            _topmostSuspended = false;
            return;
        }

        if (!_settings.General.TopmostOnlyWhenClientFocused)
        {
            if (_topmostSuspended) ResumeTopmost();
            return;
        }

        var shouldSuspend = !(_streams.ContainsKey(fg) || IsOwnAppWindow(fg));
        if (shouldSuspend == _topmostSuspended) return;

        _topmostSuspended = shouldSuspend;
        ApplyTopmost();
    }

    private static bool IsOwnAppWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        foreach (Window w in System.Windows.Application.Current.Windows)
        {
            if (new WindowInteropHelper(w).Handle == hwnd) return true;
        }
        return false;
    }
}
