using System;
using System.Collections.Generic;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// Key names as stored in settings.json ("NumPad1", "Tab", "D3", "F5" — the WPF/Avalonia
/// <c>Key</c> enum names, so the same file works on both OSes) translated to X11 keysyms.
/// Pure, no X calls, so it is unit tested with the rest of the core.
/// </summary>
public static class KeyNames
{
    // X modifier masks (X.h).
    public const uint Shift = 1 << 0;
    public const uint Control = 1 << 2;
    public const uint Alt = 1 << 3;   // Mod1
    public const uint Super = 1 << 6; // Mod4

    private static readonly Dictionary<string, uint> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = 0xff09, ["Space"] = 0x20, ["Return"] = 0xff0d, ["Enter"] = 0xff0d,
        ["Escape"] = 0xff1b, ["Back"] = 0xff08, ["Delete"] = 0xffff, ["Insert"] = 0xff63,
        ["Home"] = 0xff50, ["End"] = 0xff57, ["PageUp"] = 0xff55, ["Prior"] = 0xff55,
        ["PageDown"] = 0xff56, ["Next"] = 0xff56,
        ["Left"] = 0xff51, ["Up"] = 0xff52, ["Right"] = 0xff53, ["Down"] = 0xff54,
        ["Pause"] = 0xff13, ["Scroll"] = 0xff14,
        ["Multiply"] = 0xffaa, ["Add"] = 0xffab, ["Subtract"] = 0xffad, ["Decimal"] = 0xffae, ["Divide"] = 0xffaf,
        ["OemTilde"] = 0x60, ["Oem3"] = 0x60, ["OemMinus"] = 0x2d, ["OemPlus"] = 0x3d,
        ["OemOpenBrackets"] = 0x5b, ["Oem4"] = 0x5b, ["OemCloseBrackets"] = 0x5d, ["Oem6"] = 0x5d,
        ["OemSemicolon"] = 0x3b, ["Oem1"] = 0x3b, ["OemQuotes"] = 0x27, ["Oem7"] = 0x27,
        ["OemComma"] = 0x2c, ["OemPeriod"] = 0x2e, ["OemQuestion"] = 0x2f, ["Oem2"] = 0x2f,
        ["OemPipe"] = 0x5c, ["Oem5"] = 0x5c, ["OemBackslash"] = 0x5c,
    };

    /// <summary>Keysym of a stored key name, 0 when unknown or empty.</summary>
    public static uint KeysymOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        name = name.Trim();

        if (Named.TryGetValue(name, out var sym)) return sym;

        // Letters: keysym of the lowercase ASCII letter.
        if (name.Length == 1 && char.IsAsciiLetter(name[0])) return char.ToLowerInvariant(name[0]);

        // Top-row digits: "D0".."D9" (enum names) or plain "0".."9".
        if (name.Length == 1 && char.IsAsciiDigit(name[0])) return name[0];
        if (name.Length == 2 && (name[0] == 'D' || name[0] == 'd') && char.IsAsciiDigit(name[1])) return name[1];

        // Keypad digits: XK_KP_0 = 0xffb0. Grabbed by keycode, so they fire with NumLock on or off.
        if (name.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && name.Length == 7 && char.IsAsciiDigit(name[6]))
            return 0xffb0u + (uint)(name[6] - '0');

        // F1..F24: XK_F1 = 0xffbe.
        if ((name[0] == 'F' || name[0] == 'f') && int.TryParse(name.AsSpan(1), out var f) && f is >= 1 and <= 24)
            return 0xffbeu + (uint)(f - 1);

        return 0;
    }

    /// <summary>"Ctrl+Alt" (any separator) -> X modifier mask. "None"/empty -> 0. Same rules as the Windows build.</summary>
    public static uint ModifiersOf(string? modifiers)
    {
        uint mask = 0;
        if (string.IsNullOrEmpty(modifiers) || modifiers == "None") return mask;
        if (modifiers.Contains("Ctrl")) mask |= Control;
        if (modifiers.Contains("Alt")) mask |= Alt;
        if (modifiers.Contains("Shift")) mask |= Shift;
        if (modifiers.Contains("Win") || modifiers.Contains("Super")) mask |= Super;
        return mask;
    }

    /// <summary>Mask -> "Ctrl+Alt+Shift+Win", the form written to settings.json.</summary>
    public static string FormatModifiers(bool ctrl, bool alt, bool shift, bool win)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        return parts.Count == 0 ? "None" : string.Join("+", parts);
    }
}
