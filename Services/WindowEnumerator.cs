using System;
using System.Collections.Generic;
using System.Text;
using ClientOPreview.Models;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Services;

public static class WindowEnumerator
{
    /// <summary>Visible, not minimized, no owner, with a title — the windows worth previewing.</summary>
    public static List<WindowItem> GetTopLevelWindows(IntPtr? excludeWindow = null)
    {
        var list = new List<WindowItem>();
        var seen = new HashSet<IntPtr>();

        EnumWindows((hWnd, lParam) =>
        {
            if (excludeWindow.HasValue && hWnd == excludeWindow.Value) return true;
            if (!IsWindowVisible(hWnd)) return true;
            if (IsIconic(hWnd)) return true;
            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return true;

            var title = GetTitle(hWnd);
            if (title.Length == 0) return true;

            if (seen.Add(hWnd)) list.Add(new WindowItem { HWnd = hWnd, Title = title });
            return true;
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>Current title of a window. Empty when it has none or the handle is gone.</summary>
    public static string GetTitle(IntPtr hWnd)
    {
        try
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return string.Empty;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            AppLog.Warn("could not read a window title", ex);
            return string.Empty;
        }
    }
}
