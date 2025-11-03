using System;

namespace ClientOPreview.Models;

public class WindowItem
{
    public IntPtr HWnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Display => $"{Title}  (0x{HWnd.ToInt64():X})";
}
