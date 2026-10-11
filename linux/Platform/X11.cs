using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ClientOPreview.Linux.Native;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Linux.Native.Xlib;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// The X11 side of window management: list clients, read titles, ask the window manager to
/// activate one, and read which one is active. All through the EWMH properties, so it works
/// with any compliant WM (Cinnamon/Muffin, KDE, XFCE, GNOME on Xorg).
///
/// One connection, used from the UI thread only. The hotkey thread opens its own.
/// </summary>
internal sealed class X11 : IDisposable
{
    public IntPtr Display { get; }
    public IntPtr Root { get; }

    private readonly Dictionary<string, IntPtr> _atoms = new();

    private X11(IntPtr display)
    {
        Display = display;
        Root = XDefaultRootWindow(display);
    }

    /// <summary>Null when there is no X server to talk to (pure Wayland session, no DISPLAY).</summary>
    public static X11? Open()
    {
        var display = XOpenDisplay(IntPtr.Zero);
        return display == IntPtr.Zero ? null : new X11(display);
    }

    public void Dispose() => XCloseDisplay(Display);

    // ===== Errors =====

    // The default Xlib error handler calls exit(). A client closing while we read its pixmap
    // raises BadWindow/BadMatch, which is routine here and must not take the app down.
    private static XErrorHandler? _handler;
    private static readonly object ErrorGate = new();
    private static readonly Dictionary<IntPtr, XErrorEvent> LastError = new();

    public static void InstallErrorHandler()
    {
        _handler = OnError;
        XSetErrorHandler(_handler);
    }

    private static int OnError(IntPtr display, ref XErrorEvent e)
    {
        lock (ErrorGate) LastError[display] = e;
        return 0;
    }

    /// <summary>Drops any error recorded for this connection, before a call whose failure we want to see.</summary>
    public static void ClearError(IntPtr display)
    {
        lock (ErrorGate) LastError.Remove(display);
    }

    /// <summary>XSync, then the error the server raised since <see cref="ClearError"/>, if any.</summary>
    public static XErrorEvent? SyncError(IntPtr display)
    {
        XSync(display, False);
        lock (ErrorGate)
        {
            return LastError.Remove(display, out var e) ? e : null;
        }
    }

    // ===== Atoms and properties =====

    public IntPtr Atom(string name)
    {
        if (!_atoms.TryGetValue(name, out var atom))
        {
            atom = XInternAtom(Display, name, False);
            _atoms[name] = atom;
        }
        return atom;
    }

    /// <summary>Format-32 property as an array of longs (Xlib hands those back as C longs, 8 bytes here).</summary>
    private nint[] GetLongs(IntPtr window, string property, IntPtr type)
    {
        if (XGetWindowProperty(Display, window, Atom(property), 0, 4096, False, type,
                out _, out int format, out nuint count, out _, out IntPtr data) != Success || data == IntPtr.Zero)
            return Array.Empty<nint>();
        try
        {
            if (format != 32 || count == 0) return Array.Empty<nint>();
            var result = new nint[(int)count];
            Marshal.Copy(data, result, 0, result.Length);
            return result;
        }
        finally
        {
            XFree(data);
        }
    }

    private string GetString(IntPtr window, string property, IntPtr type)
    {
        if (XGetWindowProperty(Display, window, Atom(property), 0, 1024, False, type,
                out _, out int format, out nuint count, out _, out IntPtr data) != Success || data == IntPtr.Zero)
            return string.Empty;
        try
        {
            if (format != 8 || count == 0) return string.Empty;
            var bytes = new byte[(int)count];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            // WM_NAME is Latin-1 by the spec; _NET_WM_NAME is UTF-8.
            return type == (IntPtr)XA_STRING ? Encoding.Latin1.GetString(bytes) : Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            XFree(data);
        }
    }

    // ===== Windows =====

    public string GetTitle(IntPtr window)
    {
        var title = GetString(window, "_NET_WM_NAME", Atom("UTF8_STRING"));
        if (title.Length == 0) title = GetString(window, "WM_NAME", (IntPtr)XA_STRING);
        return title.Trim();
    }

    public bool IsAlive(IntPtr window)
    {
        ClearError(Display);
        XGetWindowAttributes(Display, window, out _);
        return SyncError(Display) == null;
    }

    public int GetPid(IntPtr window)
    {
        var pid = GetLongs(window, "_NET_WM_PID", (IntPtr)XA_CARDINAL);
        return pid.Length > 0 ? (int)pid[0] : 0;
    }

    private bool HasAtom(IntPtr window, string property, string value)
    {
        var wanted = Atom(value);
        foreach (var a in GetLongs(window, property, IntPtr.Zero))
        {
            if (a == wanted) return true;
        }
        return false;
    }

    /// <summary>Normal, titled, not minimized client windows of other processes — the ones worth previewing.</summary>
    /// <param name="exclude">This app's own windows: Avalonia does not set _NET_WM_PID, so the pid check misses them.</param>
    public List<WindowItem> GetClientWindows(ICollection<IntPtr>? exclude = null)
    {
        var list = new List<WindowItem>();
        int ownPid = Environment.ProcessId;

        foreach (var w in GetLongs(Root, "_NET_CLIENT_LIST", (IntPtr)XA_WINDOW))
        {
            var hwnd = (IntPtr)w;
            if (exclude != null && exclude.Contains(hwnd)) continue;
            if (GetPid(hwnd) == ownPid) continue;
            if (HasAtom(hwnd, "_NET_WM_WINDOW_TYPE", "_NET_WM_WINDOW_TYPE_DOCK")) continue;
            if (HasAtom(hwnd, "_NET_WM_WINDOW_TYPE", "_NET_WM_WINDOW_TYPE_DESKTOP")) continue;
            if (HasAtom(hwnd, "_NET_WM_STATE", "_NET_WM_STATE_HIDDEN")) continue;

            var title = GetTitle(hwnd);
            if (title.Length == 0) continue;
            list.Add(new WindowItem { HWnd = hwnd, Title = title });
        }
        return list;
    }

    public IntPtr GetActiveWindow()
    {
        var active = GetLongs(Root, "_NET_ACTIVE_WINDOW", (IntPtr)XA_WINDOW);
        return active.Length > 0 ? (IntPtr)active[0] : IntPtr.Zero;
    }

    /// <summary>
    /// Asks the window manager to activate (and un-minimize) a window. Source indication 2
    /// ("pager") tells the WM the request comes from a direct user action, so focus-stealing
    /// prevention lets it through — the X11 counterpart of the AttachThreadInput dance on Windows.
    /// </summary>
    public void Activate(IntPtr window)
    {
        if (window == IntPtr.Zero) return;

        // XClientMessageEvent inside a 192-byte XEvent.
        var ev = new byte[192];
        WriteInt(ev, 0, ClientMessage);
        WriteLong(ev, 16, 1);                                  // send_event
        WriteLong(ev, 32, window);                             // window
        WriteLong(ev, 40, Atom("_NET_ACTIVE_WINDOW"));         // message_type
        WriteInt(ev, 48, 32);                                  // format
        WriteLong(ev, 56, 2);                                  // l[0] source: pager
        WriteLong(ev, 64, 0);                                  // l[1] timestamp: CurrentTime
        WriteLong(ev, 72, GetActiveWindow());                  // l[2] requestor's active window

        XSendEvent(Display, Root, False, (nint)(SubstructureRedirectMask | SubstructureNotifyMask), ev);
        XFlush(Display);
    }

    /// <summary>Whole-window opacity, honoured by compositing window managers.</summary>
    public void SetOpacity(IntPtr window, double alpha)
    {
        if (window == IntPtr.Zero) return;
        alpha = Math.Clamp(alpha, 0.0, 1.0);
        var atom = Atom("_NET_WM_WINDOW_OPACITY");
        if (alpha >= 0.999)
        {
            XDeleteProperty(Display, window, atom);
        }
        else
        {
            var value = new nint[] { (nint)(uint)(alpha * uint.MaxValue) };
            XChangeProperty(Display, window, atom, (IntPtr)XA_CARDINAL, 32, PropModeReplace, value, 1);
        }
        XFlush(Display);
    }

    private static void WriteInt(byte[] buffer, int offset, int value) =>
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), value);

    private static void WriteLong(byte[] buffer, int offset, long value) =>
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 8), value);

    private static void WriteLong(byte[] buffer, int offset, IntPtr value) => WriteLong(buffer, offset, value.ToInt64());

    public static string Describe(XErrorEvent e) => $"X error {e.error_code} (request {e.request_code}.{e.minor_code})";

    public static void LogError(string context, XErrorEvent? e)
    {
        if (e is { } err) AppLog.Warn(context, Describe(err));
    }
}
