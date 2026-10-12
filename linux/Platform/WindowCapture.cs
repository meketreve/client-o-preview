using System;
using System.Runtime.InteropServices;
using ClientOPreview.Models;
using ClientOPreview.Native;
using ClientOPreview.Services;
using static ClientOPreview.Linux.Native.Xlib;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// Live picture of one client window — the Linux stand-in for a DWM thumbnail.
/// Used only from the <see cref="CaptureWorker"/> thread, on the worker's own X connection.
///
/// VRAM: a compositing WM (Cinnamon/Muffin) already keeps every window frame in an off-screen
/// pixmap to draw the desktop. We read the client's area out of that same pixmap instead of
/// redirecting the client a second time — redirecting would make the X server keep one more
/// full-size copy per client (~8 MB each at 1920x1040). Only when the frame is not redirected
/// (no compositor, or a fullscreen client the WM unredirected) do we redirect the client.
///
/// XRender scales the wanted area into a small pixmap on the server; only that small result is
/// read back. Nothing touches the game: it is a read of what the WM already draws.
/// </summary>
internal sealed class WindowCapture : IDisposable
{
    private readonly X11 _x;
    private readonly IntPtr _client;
    private readonly IntPtr _rgb24;

    // Where pixels come from: the WM frame (preferred) or the client redirected by us.
    private IntPtr _frame;
    private IntPtr _frameVisual;
    private bool _redirectedByUs;
    private int _sinceFrameCheck;

    private IntPtr _dstPixmap, _dstPicture;
    private int _dstW, _dstH;

    public WindowCapture(X11 x, IntPtr client)
    {
        _x = x;
        _client = client;
        _rgb24 = XRenderFindStandardFormat(x.Display, PictStandardRGB24);
        ResolveFrame();
    }

    /// <summary>The client window no longer exists.</summary>
    public bool Gone { get; private set; }

    /// <summary>Size of the client window at the last capture (0x0 before the first one).</summary>
    public NativeMethods.SIZE SourceSize { get; private set; }

    /// <summary>Top-level ancestor of the client: the frame the WM reparented it into.</summary>
    private void ResolveFrame()
    {
        _sinceFrameCheck = 0;
        var d = _x.Display;
        var window = _client;
        X11.ClearError(d);
        while (true)
        {
            if (XQueryTree(d, window, out var root, out var parent, out var children, out _) == 0)
            {
                Gone = true;
                return;
            }
            if (children != IntPtr.Zero) XFree(children);
            if (parent == IntPtr.Zero || parent == root) break;
            window = parent;
        }

        if (window != _frame)
        {
            _frame = window;
            XGetWindowAttributes(d, _frame, out var fa);
            _frameVisual = fa.visual;
        }
    }

    /// <summary>
    /// Renders the client — cropped by <paramref name="region"/>, zoomed when asked — scaled into
    /// an area of areaW x areaH (letterboxed when the region keeps its proportions). Writes BGRX
    /// rows into <paramref name="buffer"/>; returns the size written, or null when there is no
    /// new frame (client unmapped, gone, or the server refused).
    /// </summary>
    public (int W, int H)? Capture(int areaW, int areaH, RegionPreset? region, bool zoomed, Zoom zoom, ref byte[] buffer)
    {
        if (Gone || areaW < 2 || areaH < 2) return null;
        var d = _x.Display;

        X11.ClearError(d);
        XGetWindowAttributes(d, _client, out var attr);
        if (X11.TakeError(d) != null)
        {
            Gone = true;
            return null;
        }
        if (attr.map_state != IsViewable || attr.width <= 0 || attr.height <= 0) return null;

        if (++_sinceFrameCheck > 50) ResolveFrame(); // reparenting is rare; re-check now and then

        var src = new NativeMethods.SIZE { cx = attr.width, cy = attr.height };
        SourceSize = src;
        var crop = ThumbnailGeometry.CropSource(src, region, zoomed, zoom);
        var dest = new NativeMethods.RECT { Left = 0, Top = 0, Right = areaW, Bottom = areaH };
        if (ThumbnailGeometry.NeedsCrop(region) && region!.LockAspect)
            dest = ThumbnailGeometry.Letterbox(dest, crop.Right - crop.Left, crop.Bottom - crop.Top);
        int w = dest.Right - dest.Left, h = dest.Bottom - dest.Top;

        // Client's top-left inside the pixmap we read from.
        int ox = 0, oy = 0;
        IntPtr drawable = _client, visual = attr.visual;
        if (!_redirectedByUs && _frame != IntPtr.Zero && _frame != _client)
        {
            XTranslateCoordinates(d, _client, _frame, 0, 0, out ox, out oy, out _);
            drawable = _frame;
            visual = _frameVisual;
        }

        EnsureDestination(w, h);

        IntPtr pixmap = IntPtr.Zero, picture = IntPtr.Zero, image = IntPtr.Zero;
        try
        {
            X11.ClearError(d);
            pixmap = XCompositeNameWindowPixmap(d, drawable);
            var format = XRenderFindVisualFormat(d, visual);
            if (pixmap == IntPtr.Zero || format == IntPtr.Zero) return null;

            picture = XRenderCreatePicture(d, pixmap, format, 0, IntPtr.Zero);
            double cw = Math.Max(1, crop.Right - crop.Left);
            double ch = Math.Max(1, crop.Bottom - crop.Top);
            var transform = XTransform.ScaleTranslate(cw / w, ch / h, ox + crop.Left, oy + crop.Top);
            XRenderSetPictureTransform(d, picture, ref transform);
            XRenderSetPictureFilter(d, picture, "bilinear", IntPtr.Zero, 0);
            XRenderComposite(d, PictOpSrc, picture, IntPtr.Zero, _dstPicture, 0, 0, 0, 0, 0, 0, (uint)w, (uint)h);

            // A round trip: every error caused by the requests above has arrived when it returns.
            image = XGetImage(d, _dstPixmap, 0, 0, (uint)w, (uint)h, unchecked((nuint)AllPlanes), ZPixmap);
            if (X11.TakeError(d) is { } err || image == IntPtr.Zero)
            {
                // BadMatch on the frame: it is not redirected (no compositor, or a fullscreen client
                // the WM unredirected). Fall back to redirecting the client ourselves.
                if (!_redirectedByUs && drawable == _frame) RedirectClient();
                return null;
            }

            var img = Marshal.PtrToStructure<XImage>(image);
            if (img.bits_per_pixel != 32) return null;
            int stride = w * 4;
            if (buffer.Length < stride * h) buffer = new byte[stride * h];
            for (int y = 0; y < h; y++)
                Marshal.Copy(img.data + y * img.bytes_per_line, buffer, y * stride, stride);
            // The 4th byte of a depth-24 image is padding (usually 0). The preview window is ARGB,
            // so a 0 there would make the compositor show the desktop through the thumbnail.
            for (int i = 3; i < stride * h; i += 4) buffer[i] = 0xFF;
            return (w, h);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"capture 0x{_client.ToInt64():X}", ex);
            return null;
        }
        finally
        {
            if (image != IntPtr.Zero) XDestroyImage(image);
            if (picture != IntPtr.Zero) XRenderFreePicture(d, picture);
            if (pixmap != IntPtr.Zero) XFreePixmap(d, pixmap);
        }
    }

    private void RedirectClient()
    {
        X11.ClearError(_x.Display);
        XCompositeRedirectWindow(_x.Display, _client, CompositeRedirectAutomatic);
        var err = X11.SyncError(_x.Display);
        _redirectedByUs = err == null;
        AppLog.Info("Capture", $"0x{_client.ToInt64():X}: WM frame not readable, redirecting the client ({(_redirectedByUs ? "ok" : X11.Describe(err!.Value))})");
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
        if (_redirectedByUs)
        {
            X11.ClearError(_x.Display);
            XCompositeUnredirectWindow(_x.Display, _client, CompositeRedirectAutomatic);
            X11.SyncError(_x.Display); // the client may already be gone; nothing to do about it
            _redirectedByUs = false;
        }
        XFlush(_x.Display);
    }
}
