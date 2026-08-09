using System;
using System.Globalization;
using System.Text;

namespace ClientOPreview.Services;

/// <summary>
/// Keys and geometry strings used to persist where each preview lives.
/// Free of WPF and of Win32 so it can be unit tested on any OS.
/// </summary>
public static class LayoutKey
{
    public const string Shared = "default";

    private static readonly char[] Invalid = { '"', '\\', '/', '\n', '\r', '\t' };

    /// <summary>Strips characters that would be awkward inside a JSON key.</summary>
    public static string Sanitize(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        var result = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (Array.IndexOf(Invalid, c) < 0) result.Append(c);
        }
        return result.ToString().Trim();
    }

    /// <summary>
    /// Key of one preview. <paramref name="occurrence"/> separates several clients that
    /// share a title; it is allocated per live window (see StreamManager) so two open
    /// previews can never collide on the same key.
    /// </summary>
    public static string For(string title, int occurrence)
    {
        var clean = Sanitize(title);
        return string.IsNullOrEmpty(clean) ? Shared : $"title:{occurrence}:{clean}";
    }

    /// <summary>Key written by versions that had no occurrence index.</summary>
    public static string LegacyFor(string title)
    {
        var clean = Sanitize(title);
        return string.IsNullOrEmpty(clean) ? Shared : $"title:{clean}";
    }

    /// <summary>Geometry is stored as WIDTHxHEIGHT+LEFT+TOP.</summary>
    public static string FormatGeometry(double left, double top, double width, double height)
    {
        var w = Math.Max(1, (int)Math.Round(width));
        var h = Math.Max(1, (int)Math.Round(height));
        var l = (int)Math.Round(left);
        var t = (int)Math.Round(top);
        return string.Create(CultureInfo.InvariantCulture, $"{w}x{h}+{l}+{t}");
    }

    public static bool TryParseGeometry(string? value, out int left, out int top, out int width, out int height)
    {
        left = top = width = height = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split('+');
        if (parts.Length != 3) return false;

        var size = parts[0].Split('x');
        if (size.Length != 2) return false;

        return int.TryParse(size[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
            && int.TryParse(size[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out left)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out top)
            && width > 0 && height > 0;
    }
}
