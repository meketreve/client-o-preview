using System;
using System.Runtime.InteropServices;
using System.Text;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Services;

public static class WindowEnumerator
{
    public static System.Collections.Generic.List<ClientOPreview.Models.WindowItem> GetTopLevelWindows(IntPtr? excludeWindow = null)
    {
        var list = new System.Collections.Generic.List<ClientOPreview.Models.WindowItem>();
        var seen = new System.Collections.Generic.HashSet<IntPtr>();

        EnumWindows((hWnd, lParam) =>
        {
            if (excludeWindow.HasValue && hWnd == excludeWindow.Value) return true;
            if (!IsWindowVisible(hWnd)) return true;
            if (IsIconic(hWnd)) return true;
            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return true;
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (title.Length == 0) return true;
            if (!seen.Contains(hWnd))
            {
                seen.Add(hWnd);
                list.Add(new ClientOPreview.Models.WindowItem { HWnd = hWnd, Title = title });
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }
}
