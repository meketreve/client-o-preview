using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
    private readonly List<string> _failed = new();
    private HwndSource? _source;
    private Hotkeys _hotkeys = new();
    private bool _registered;

    public HotkeyManager(Window host) => _host = host;

    /// <summary>Cycle hotkey pressed.</summary>
    public event EventHandler? CycleRequested;

    /// <summary>Direct hotkey pressed, with its 0-based index.</summary>
    public event EventHandler<int>? DirectRequested;

    /// <summary>Combos Windows refused on the last <see cref="Reload"/>, e.g. "Alt+Tab". Empty when all took.</summary>
    public IReadOnlyList<string> FailedCombos => _failed;

    /// <summary>Call once the host window has a handle (Loaded).</summary>
    public void Attach(Hotkeys hotkeys)
    {
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(Hook);
        AppLog.Info("Hotkeys", $"app running elevated: {IsElevated()}");
        Reload(hotkeys);
    }

    /// <summary>An elevated client cannot be activated by a non-elevated app (UIPI), so this is worth knowing.</summary>
    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Hotkeys", ex);
            return false;
        }
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
        _failed.Clear();
        if (!_hotkeys.Enabled) return;
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;

        var cycleVk = VirtualKeyOf(_hotkeys.CycleKey);
        if (cycleVk == 0)
            LogUnknownKey("cycle", _hotkeys.CycleKey);
        else
            LogRegister("cycle", _hotkeys.CycleModifiers, _hotkeys.CycleKey,
                RegisterHotKey(hwnd, IdCycle, ParseModifiers(_hotkeys.CycleModifiers) | MOD_NOREPEAT, cycleVk));

        var directMods = ParseModifiers(_hotkeys.DirectModifiers) | MOD_NOREPEAT;
        for (int i = 0; i < _hotkeys.DirectKeys.Count && i < DirectCount; i++)
        {
            var vk = VirtualKeyOf(_hotkeys.DirectKeys[i]);
            if (vk == 0)
                LogUnknownKey($"direct[{i}]", _hotkeys.DirectKeys[i]);
            else
                LogRegister($"direct[{i}]", _hotkeys.DirectModifiers, _hotkeys.DirectKeys[i],
                    RegisterHotKey(hwnd, IdDirectBase + i, directMods, vk));
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
            // Logged because "nothing happened" has two very different causes: the hotkey never
            // reached us (the focused app ate the key), or it did and the focus call was refused.
            AppLog.Info("Hotkeys", "cycle pressed");
            CycleRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id >= IdDirectBase && id < IdDirectBase + DirectCount)
        {
            AppLog.Info("Hotkeys", $"direct[{id - IdDirectBase}] pressed");
            DirectRequested?.Invoke(this, id - IdDirectBase);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // Windows refuses combos it reserves (Alt+Tab) or that another app already holds, and the
    // refusal used to be silent — a hotkey that "does nothing" looked identical to a focus bug.
    private void LogRegister(string what, string? modifiers, string? key, bool ok)
    {
        var combo = string.IsNullOrEmpty(modifiers) || modifiers == "None" ? key ?? "" : $"{modifiers}+{key}";
        if (ok)
        {
            AppLog.Info("Hotkeys", $"{what} registered: {combo}");
            return;
        }

        AppLog.Warn("Hotkeys", $"{what} NOT registered: {combo} (win32 error {Marshal.GetLastWin32Error()})");
        if (!_failed.Contains(combo)) _failed.Add(combo);
    }

    private void LogUnknownKey(string what, string? key)
    {
        if (string.IsNullOrEmpty(key)) return;
        AppLog.Warn("Hotkeys", $"{what}: unknown key name '{key}'");
        if (!_failed.Contains(key)) _failed.Add(key);
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
