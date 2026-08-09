using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClientOPreview.Models;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Services;

/// <summary>
/// Owns the global hotkeys: registration with Windows, the WM_HOTKEY hook and the
/// translation from the strings stored in settings to virtual key codes.
/// Reports intent through events; what to focus is the caller's business.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    public const int DirectCount = 10;

    private const int IdCycle = 1;
    private const int IdDirectBase = 100;

    private readonly Window _host;
    private HwndSource? _source;
    private Hotkeys _hotkeys = new();
    private bool _registered;

    public HotkeyManager(Window host) => _host = host;

    /// <summary>Cycle hotkey pressed.</summary>
    public event EventHandler? CycleRequested;

    /// <summary>Direct hotkey pressed, with its 0-based index.</summary>
    public event EventHandler<int>? DirectRequested;

    /// <summary>Call once the host window has a handle (Loaded).</summary>
    public void Attach(Hotkeys hotkeys)
    {
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(Hook);
        Reload(hotkeys);
    }

    public void Reload(Hotkeys hotkeys)
    {
        _hotkeys = hotkeys;
        Unregister();
        Register();
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(Hook);
        _source = null;
    }

    private IntPtr Handle => new WindowInteropHelper(_host).Handle;

    private void Register()
    {
        if (!_hotkeys.Enabled) return;
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;

        var cycleVk = VirtualKeyOf(_hotkeys.CycleKey);
        if (cycleVk != 0)
            RegisterHotKey(hwnd, IdCycle, ParseModifiers(_hotkeys.CycleModifiers) | MOD_NOREPEAT, cycleVk);

        var directMods = ParseModifiers(_hotkeys.DirectModifiers) | MOD_NOREPEAT;
        for (int i = 0; i < _hotkeys.DirectKeys.Count && i < DirectCount; i++)
        {
            var vk = VirtualKeyOf(_hotkeys.DirectKeys[i]);
            if (vk != 0) RegisterHotKey(hwnd, IdDirectBase + i, directMods, vk);
        }
        _registered = true;
    }

    private void Unregister()
    {
        if (!_registered) return;
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;

        UnregisterHotKey(hwnd, IdCycle);
        for (int i = 0; i < DirectCount; i++) UnregisterHotKey(hwnd, IdDirectBase + i);
        _registered = false;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        int id = wParam.ToInt32();
        if (id == IdCycle)
        {
            CycleRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id >= IdDirectBase && id < IdDirectBase + DirectCount)
        {
            DirectRequested?.Invoke(this, id - IdDirectBase);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>"Alt+Ctrl" -> MOD_ALT | MOD_CONTROL. "None"/empty -> no modifier.</summary>
    internal static uint ParseModifiers(string? modifiers)
    {
        uint mods = MOD_NONE;
        if (string.IsNullOrEmpty(modifiers) || modifiers == "None") return mods;

        if (modifiers.Contains("Alt")) mods |= MOD_ALT;
        if (modifiers.Contains("Ctrl")) mods |= MOD_CONTROL;
        if (modifiers.Contains("Shift")) mods |= MOD_SHIFT;
        if (modifiers.Contains("Win")) mods |= MOD_WIN;
        return mods;
    }

    /// <summary>Key name as stored in settings ("NumPad1") -> virtual key code. 0 when unknown.</summary>
    internal static uint VirtualKeyOf(string? keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return 0;
        return Enum.TryParse<Key>(keyName, true, out var key)
            ? (uint)KeyInterop.VirtualKeyFromKey(key)
            : 0;
    }
}
