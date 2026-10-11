using System;
using System.Runtime.InteropServices;
using ClientOPreview.Models;
using ClientOPreview.Native;
using ClientOPreview.Services;
using static ClientOPreview.Linux.Native.Xlib;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// Live picture of one client window — the Linux stand-in for a DWM thumbnail.
///
/// The client is redirected off-screen with XComposite (the compositor keeps drawing it as
/// usual), XRender scales the region we want into a small pixmap on the X server, and only
/// that small result is read back. So a 1080p client costs a 320x180 copy per frame, not 8 MB.
/// Nothing touches the game itself: same as on Windows, it is a read of what the WM already draws.
/// </summary>
internal sealed class WindowCapture : IDisposable
{
    private readonly X11 _x;
    private readonly IntPtr _source;
    private readonly IntPtr _rgb24;
    private bool _redirected;

    // Destination pixmap/picture, rebuilt when the preview size changes.
    private IntPtr _dstPixmap, _dstPicture;
    private int _dstW, _dstH;

    public WindowCapture(X11 x, IntPtr source)
    {
        _x = x;
        _source = source;
        _rgb24 = XRenderFindStandardFormat(x.Display, PictStandardRGB24);

        X11.ClearError(x.Display);
        XCompositeRedirectWindow(x.Display, source, CompositeRedirectAutomatic);
        var err = X11.SyncError(x.Display);
        _redirected = err == null;
        X11.LogError($"redirect 0x{source.ToInt64():X}", err);
    }

    /// <summary>Size of the client window; empty while it is unmapped (minimized, other workspace).</summary>
    public bool TryGetSourceSize(out NativeMethods.SIZE size)
    {
        size = default;
        X11.ClearError(_x.Display);
        XGetWindowAttributes(_x.Display, _source, out var attr);
        if (X11.SyncError(_x.Display) != null || attr.map_state != IsViewable) return false;
        size = new NativeMethods.SIZE { cx = attr.width, cy = attr.height };
        return size.cx > 0 && size.cy > 0;
    }

    /// <summary>
    /// Renders <paramref name="crop"/> of the client, scaled to width x height, as BGRX pixels
    /// into <paramref name="target"/> (stride = width * 4). False when there is no frame to show
    /// (client unmapped or gone) — the caller keeps the previous frame.
    /// </summary>
    public bool Capture(NativeMethods.RECT crop, int width, int height, IntPtr target)
    {
        if (!_redirected || width <= 0 || height <= 0) return false;
        var d = _x.Display;

        X11.ClearError(d);
        XGetWindowAttributes(d, _source, out var attr);
        if (X11.SyncError(d) != null || attr.map_state != IsViewable) return false;

        EnsureDestination(width, height);

        // The window pixmap is reallocated by the server whenever the client resizes, so it is
        // named again every frame; that is cheap compared to the copy.
        IntPtr pixmap = IntPtr.Zero, picture = IntPtr.Zero, image = IntPtr.Zero;
        try
        {
            pixmap = XCompositeNameWindowPixmap(d, _source);
            var format = XRenderFindVisualFormat(d, attr.visual);
            if (pixmap == IntPtr.Zero || format == IntPtr.Zero) return false;

            picture = XRenderCreatePicture(d, pixmap, format, 0, IntPtr.Zero);

            double cw = Math.Max(1, crop.Right - crop.Left);
            double ch = Math.Max(1, crop.Bottom - crop.Top);
            var transform = XTransform.ScaleTranslate(cw / width, ch / height, crop.Left, crop.Top);
            XRenderSetPictureTransform(d, picture, ref transform);
            XRenderSetPictureFilter(d, picture, "good", IntPtr.Zero, 0);

            XRenderComposite(d, PictOpSrc, picture, IntPtr.Zero, _dstPicture, 0, 0, 0, 0, 0, 0, (uint)width, (uint)height);

            image = XGetImage(d, _dstPixmap, 0, 0, (uint)width, (uint)height, unchecked((nuint)AllPlanes), ZPixmap);
            var err = X11.SyncError(d);
            if (err != null || image == IntPtr.Zero) return false;

            var img = Marshal.PtrToStructure<XImage>(image);
            if (img.bits_per_pixel != 32) return false;
            CopyRows(img.data, img.bytes_per_line, target, width * 4, height);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"capture 0x{_source.ToInt64():X}", ex);
            return false;
        }
        finally
        {
            if (image != IntPtr.Zero) XDestroyImage(image);
            if (picture != IntPtr.Zero) XRenderFreePicture(d, picture);
            if (pixmap != IntPtr.Zero) XFreePixmap(d, pixmap);
        }
    }

    private static unsafe void CopyRows(IntPtr src, int srcStride, IntPtr dst, int dstStride, int rows)
    {
        int bytes = Math.Min(srcStride, dstStride);
        for (int y = 0; y < rows; y++)
        {
            Buffer.MemoryCopy((byte*)src + (long)y * srcStride, (byte*)dst + (long)y * dstStride, dstStride, bytes);
        }
    }

    private void EnsureDestination(int width, int height)
    {
        if (_dstPixmap != IntPtr.Zero && width == _dstW && height == _dstH) return;
        FreeDestination();
        _dstPixmap = XCreatePixmap(_x.Display, _x.Root, (uint)width, (uint)height, 24);
        _dstPicture = XRenderCreatePicture(_x.Display, _dstPixmap, _rgb24, 0, IntPtr.Zero);
        _dstW = width;
        _dstH = height;
    }

    private void FreeDestination()
    {
        if (_dstPicture != IntPtr.Zero) XRenderFreePicture(_x.Display, _dstPicture);
        if (_dstPixmap != IntPtr.Zero) XFreePixmap(_x.Display, _dstPixmap);
        _dstPicture = _dstPixmap = IntPtr.Zero;
    }

    public void Dispose()
    {
        FreeDestination();
        if (_redirected)
        {
            X11.ClearError(_x.Display);
            XCompositeUnredirectWindow(_x.Display, _source, CompositeRedirectAutomatic);
            X11.SyncError(_x.Display); // the client may already be gone; nothing to do about it
            _redirected = false;
        }
    }
}
