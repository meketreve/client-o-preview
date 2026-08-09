using System;
using ClientOPreview.Models;
using static ClientOPreview.Native.NativeMethods;

namespace ClientOPreview.Services;

/// <summary>
/// Turns a region preset plus the hover zoom into the rectangles DWM expects.
/// Pure math, no WPF and no Win32 calls, so it is unit tested on any OS.
/// </summary>
internal static class ThumbnailGeometry
{
    public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

    /// <summary>A preset that covers the whole window is the same as having no crop at all.</summary>
    public static bool IsFullWindow(RegionPreset r)
        => r.X <= 0.0001 && r.Y <= 0.0001 && r.W >= 0.9999 && r.H >= 0.9999;

    public static bool NeedsCrop(RegionPreset? region) => region != null && !IsFullWindow(region);

    /// <summary>
    /// Source rectangle of the thumbnail: the region decides *what part of the client we watch*,
    /// then the hover zoom magnifies inside that region.
    /// </summary>
    public static RECT CropSource(SIZE src, RegionPreset? region, bool applyZoom, Zoom zoom)
    {
        double left = src.cx * (region?.X ?? 0.0);
        double top = src.cy * (region?.Y ?? 0.0);
        double cw = Math.Max(1.0, src.cx * (region?.W ?? 1.0));
        double ch = Math.Max(1.0, src.cy * (region?.H ?? 1.0));

        if (applyZoom)
        {
            double mag = Math.Max(1.0, zoom.Magnification);
            double zw = cw / mag;
            double zh = ch / mag;
            left += (cw - zw) * zoom.OffsetX;
            top += (ch - zh) * zoom.OffsetY;
            cw = zw;
            ch = zh;
        }

        int sl = Clamp((int)Math.Round(left), 0, Math.Max(0, src.cx - 1));
        int st = Clamp((int)Math.Round(top), 0, Math.Max(0, src.cy - 1));
        int sr = Clamp(sl + (int)Math.Round(cw), sl + 1, src.cx);
        int sb = Clamp(st + (int)Math.Round(ch), st + 1, src.cy);
        return new RECT { Left = sl, Top = st, Right = sr, Bottom = sb };
    }

    /// <summary>Centres the crop inside the destination keeping its proportions, instead of stretching it.</summary>
    public static RECT Letterbox(RECT dest, int srcW, int srcH)
    {
        int dw = dest.Right - dest.Left;
        int dh = dest.Bottom - dest.Top;
        if (dw <= 0 || dh <= 0 || srcW <= 0 || srcH <= 0) return dest;

        double scale = Math.Min((double)dw / srcW, (double)dh / srcH);
        int nw = Math.Max(1, (int)Math.Round(srcW * scale));
        int nh = Math.Max(1, (int)Math.Round(srcH * scale));
        int ox = dest.Left + (dw - nw) / 2;
        int oy = dest.Top + (dh - nh) / 2;
        return new RECT { Left = ox, Top = oy, Right = ox + nw, Bottom = oy + nh };
    }
}
