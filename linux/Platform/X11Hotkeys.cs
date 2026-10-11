using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ClientOPreview.Models;
using ClientOPreview.Services;
using static ClientOPreview.Linux.Native.Xlib;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// Global hotkeys via XGrabKey on the root window — the Linux counterpart of
/// Services/HotkeyManager.cs. Runs its own X connection on a background thread; events are
/// raised on that thread, the caller marshals them to the UI.
///
/// Known limit, same nature as on Windows: a fullscreen client that grabs the whole keyboard
/// (XGrabKeyboard) receives every key itself, and no passive grab fires while it holds it.
/// </summary>
internal sealed class X11Hotkeys : IDisposable
{
    public const int DirectCount = 10;

    private readonly IntPtr _display;
    private readonly IntPtr _root;
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _work = new();
    private volatile bool _running = true;

    // (keycode, modifiers) -> action id. -1 = cycle, 0..9 = direct.
    private readonly Dictionary<(int, uint), int> _bindings = new();
    // Keys held down: with detectable auto-repeat, a held key sends presses but no release in
    // between, so this is how MOD_NOREPEAT is reproduced.
    private readonly HashSet<int> _down = new();

    // NumLock and CapsLock change the modifier state, so each combo is grabbed four times.
    private static readonly uint[] LockVariants = { 0, LockMask, Mod2Mask, LockMask | Mod2Mask };

    public event EventHandler? CycleRequested;
    public event EventHandler<int>? DirectRequested;

    /// <summary>Raised after each <see cref="Reload"/> with the combos the X server refused.</summary>
    public event EventHandler<IReadOnlyList<string>>? Registered;

    public X11Hotkeys()
    {
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) throw new InvalidOperationException("cannot open X display for hotkeys");
        _root = XDefaultRootWindow(_display);
        XkbSetDetectableAutoRepeat(_display, True, out _);
        XSelectInput(_display, _root, (nint)(KeyPressMask | KeyReleaseMask));

        _thread = new Thread(Loop) { IsBackground = true, Name = "x11-hotkeys" };
        _thread.Start();
    }

    public void Reload(Hotkeys hotkeys)
    {
        // Snapshot now: the settings object belongs to the UI thread.
        var enabled = hotkeys.Enabled;
        var cycle = (hotkeys.CycleModifiers, hotkeys.CycleKey);
        var directMods = hotkeys.DirectModifiers;
        var directKeys = new List<string>(hotkeys.DirectKeys);
        _work.Enqueue(() => Register(enabled, cycle, directMods, directKeys));
    }

    private void Register(bool enabled, (string mods, string key) cycle, string directMods, List<string> directKeys)
    {
        UngrabAll();
        var failed = new List<string>();
        if (enabled)
        {
            Grab(-1, cycle.mods, cycle.key, failed);
            for (int i = 0; i < directKeys.Count && i < DirectCount; i++) Grab(i, directMods, directKeys[i], failed);
        }
        Registered?.Invoke(this, failed);
    }

    private void Grab(int id, string? modifiers, string? key, List<string> failed)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var what = id < 0 ? "cycle" : $"direct[{id}]";
        var combo = string.IsNullOrEmpty(modifiers) || modifiers == "None" ? key : $"{modifiers}+{key}";

        var sym = KeyNames.KeysymOf(key);
        int code = sym == 0 ? 0 : XKeysymToKeycode(_display, sym);
        if (code == 0)
        {
            AppLog.Warn("Hotkeys", $"{what}: unknown key name '{key}'");
            failed.Add(key);
            return;
        }

        uint mods = KeyNames.ModifiersOf(modifiers);
        X11.ClearError(_display);
        foreach (var extra in LockVariants)
            XGrabKey(_display, code, mods | extra, _root, False, GrabModeAsync, GrabModeAsync);

        // BadAccess: another client (the desktop's own shortcuts, another app) already holds it.
        if (X11.SyncError(_display) is { } err)
        {
            AppLog.Warn("Hotkeys", $"{what} NOT registered: {combo} ({X11.Describe(err)})");
            foreach (var extra in LockVariants) XUngrabKey(_display, code, mods | extra, _root);
            X11.SyncError(_display);
            failed.Add(combo);
            return;
        }

        _bindings[(code, mods)] = id;
        AppLog.Info("Hotkeys", $"{what} registered: {combo}");
    }

    private void UngrabAll()
    {
        foreach (var (code, mods) in _bindings.Keys)
        {
            foreach (var extra in LockVariants) XUngrabKey(_display, code, mods | extra, _root);
        }
        _bindings.Clear();
        _down.Clear();
        X11.SyncError(_display);
    }

    private void Loop()
    {
        var ev = Marshal.AllocHGlobal(192);
        try
        {
            while (_running)
            {
                while (_work.TryDequeue(out var job))
                {
                    try { job(); }
                    catch (Exception ex) { AppLog.Error("hotkey registration", ex); }
                }

                while (_running && XPending(_display) > 0)
                {
                    XNextEvent(_display, ev);
                    Dispatch(Marshal.PtrToStructure<XKeyEvent>(ev));
                }

                Thread.Sleep(10);
            }
            UngrabAll();
        }
        catch (Exception ex)
        {
            AppLog.Error("hotkey loop", ex);
        }
        finally
        {
            Marshal.FreeHGlobal(ev);
            XCloseDisplay(_display);
        }
    }

    private void Dispatch(XKeyEvent key)
    {
        int code = (int)key.keycode;
        if (key.type == KeyRelease)
        {
            _down.Remove(code);
            return;
        }
        if (key.type != KeyPress || !_down.Add(code)) return;

        uint mods = key.state & (ShiftMask | ControlMask | Mod1Mask | Mod4Mask);
        if (!_bindings.TryGetValue((code, mods), out var id)) return;

        try
        {
            if (id < 0)
            {
                AppLog.Info("Hotkeys", "cycle pressed");
                CycleRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                AppLog.Info("Hotkeys", $"direct[{id}] pressed");
                DirectRequested?.Invoke(this, id);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("hotkey handler", ex);
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread.Join(500);
    }
}
