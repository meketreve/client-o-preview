using System;
using System.Runtime.InteropServices;

namespace ClientOPreview.Linux.Native;

/// <summary>
/// Every P/Invoke of the Linux build: Xlib, Composite and Render. The Linux counterpart of
/// Native/NativeMethods.cs. Struct layouts are the 64-bit ones (long = 8 bytes).
/// </summary>
internal static class Xlib
{
    private const string X11 = "libX11.so.6";
    private const string Xcomposite = "libXcomposite.so.1";
    private const string Xrender = "libXrender.so.1";

    // ===== Constants =====

    public const int Success = 0;
    public const int False = 0;
    public const int True = 1;

    public const int IsViewable = 2;                 // XWindowAttributes.map_state
    public const int ZPixmap = 2;
    public const ulong AllPlanes = ulong.MaxValue;

    public const int KeyPress = 2;
    public const int KeyRelease = 3;
    public const int ClientMessage = 33;
    public const int GrabModeAsync = 1;

    public const long KeyPressMask = 1L << 0;
    public const long KeyReleaseMask = 1L << 1;
    public const long SubstructureNotifyMask = 1L << 19;
    public const long SubstructureRedirectMask = 1L << 20;

    public const uint ShiftMask = 1 << 0;
    public const uint LockMask = 1 << 1;
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask = 1 << 3;   // Alt
    public const uint Mod2Mask = 1 << 4;   // NumLock
    public const uint Mod4Mask = 1 << 6;   // Super / Win

    public const int CompositeRedirectAutomatic = 0;

    public const int PictOpSrc = 1;
    public const int PictStandardRGB24 = 1;

    public const int XA_CARDINAL = 6;
    public const int XA_WINDOW = 33;
    public const int XA_STRING = 31;
    public const int PropModeReplace = 0;

    public const byte X_GrabKey = 33;

    // ===== Structs =====

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public int x, y, width, height, border_width, depth;
        public IntPtr visual;
        public IntPtr root;
        public int c_class, bit_gravity, win_gravity, backing_store;
        public nuint backing_planes, backing_pixel;
        public int save_under;
        public IntPtr colormap;
        public int map_installed, map_state;
        public nint all_event_masks, your_event_mask, do_not_propagate_mask;
        public int override_redirect;
        public IntPtr screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public nuint resourceid;
        public nuint serial;
        public byte error_code, request_code, minor_code;
    }

    /// <summary>XImage, only the leading fields we read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int width, height, xoffset, format;
        public IntPtr data;
        public int byte_order, bitmap_unit, bitmap_bit_order, bitmap_pad, depth, bytes_per_line, bits_per_pixel;
    }

    /// <summary>XKeyEvent, enough of it to read the key and its modifiers.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XKeyEvent
    {
        public int type;
        public nuint serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window, root, subwindow;
        public nuint time;
        public int x, y, x_root, y_root;
        public uint state, keycode;
        public int same_screen;
    }

    /// <summary>XTransform: 3x3 matrix of 16.16 fixed point.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XTransform
    {
        public int m00, m01, m02, m10, m11, m12, m20, m21, m22;

        public static int Fixed(double v) => (int)Math.Round(v * 65536.0);

        /// <summary>Maps a destination pixel to (offset + d * scale) in the source.</summary>
        public static XTransform ScaleTranslate(double sx, double sy, double ox, double oy) => new()
        {
            m00 = Fixed(sx), m01 = 0, m02 = Fixed(ox),
            m10 = 0, m11 = Fixed(sy), m12 = Fixed(oy),
            m20 = 0, m21 = 0, m22 = Fixed(1.0)
        };
    }

    public delegate int XErrorHandler(IntPtr display, ref XErrorEvent error);

    // ===== Xlib =====

    [DllImport(X11)] public static extern int XInitThreads();
    [DllImport(X11)] public static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport(X11)] public static extern int XCloseDisplay(IntPtr display);
    [DllImport(X11)] public static extern IntPtr XSetErrorHandler(XErrorHandler handler);
    [DllImport(X11)] public static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport(X11)] public static extern int XFlush(IntPtr display);
    [DllImport(X11)] public static extern int XSync(IntPtr display, int discard);
    [DllImport(X11)] public static extern int XPending(IntPtr display);
    [DllImport(X11)] public static extern int XNextEvent(IntPtr display, IntPtr eventReturn);
    [DllImport(X11)] public static extern int XFree(IntPtr data);
    [DllImport(X11)] public static extern int XDestroyImage(IntPtr image);

    [DllImport(X11)] public static extern IntPtr XInternAtom(IntPtr display, string name, int onlyIfExists);

    [DllImport(X11)]
    public static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
        nint offset, nint length, int delete, IntPtr reqType,
        out IntPtr actualType, out int actualFormat, out nuint nitems, out nuint bytesAfter, out IntPtr prop);

    [DllImport(X11)]
    public static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, nint[] data, int nelements);

    [DllImport(X11)] public static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

    [DllImport(X11)] public static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

    [DllImport(X11)] public static extern int XSendEvent(IntPtr display, IntPtr window, int propagate, nint eventMask, byte[] eventSend);

    [DllImport(X11)] public static extern IntPtr XCreatePixmap(IntPtr display, IntPtr drawable, uint width, uint height, uint depth);
    [DllImport(X11)] public static extern int XFreePixmap(IntPtr display, IntPtr pixmap);

    [DllImport(X11)]
    public static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height,
        nuint planeMask, int format);

    [DllImport(X11)] public static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);

    [DllImport(X11)]
    public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow,
        int ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(X11)] public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);
    [DllImport(X11)] public static extern int XSelectInput(IntPtr display, IntPtr window, nint mask);
    [DllImport(X11)] public static extern int XkbSetDetectableAutoRepeat(IntPtr display, int detectable, out int supported);

    [DllImport(X11)]
    public static extern int XTranslateCoordinates(IntPtr display, IntPtr src, IntPtr dest, int srcX, int srcY,
        out int destX, out int destY, out IntPtr child);

    [DllImport(X11)]
    public static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent,
        out IntPtr children, out uint nchildren);

    // ===== Composite =====

    [DllImport(Xcomposite)] public static extern int XCompositeQueryExtension(IntPtr display, out int eventBase, out int errorBase);
    [DllImport(Xcomposite)] public static extern void XCompositeRedirectWindow(IntPtr display, IntPtr window, int update);
    [DllImport(Xcomposite)] public static extern void XCompositeUnredirectWindow(IntPtr display, IntPtr window, int update);
    [DllImport(Xcomposite)] public static extern IntPtr XCompositeNameWindowPixmap(IntPtr display, IntPtr window);

    // ===== Render =====

    [DllImport(Xrender)] public static extern IntPtr XRenderFindVisualFormat(IntPtr display, IntPtr visual);
    [DllImport(Xrender)] public static extern IntPtr XRenderFindStandardFormat(IntPtr display, int format);

    [DllImport(Xrender)]
    public static extern IntPtr XRenderCreatePicture(IntPtr display, IntPtr drawable, IntPtr format, nuint valueMask, IntPtr attributes);

    [DllImport(Xrender)] public static extern void XRenderFreePicture(IntPtr display, IntPtr picture);
    [DllImport(Xrender)] public static extern void XRenderSetPictureTransform(IntPtr display, IntPtr picture, ref XTransform transform);
    [DllImport(Xrender)] public static extern void XRenderSetPictureFilter(IntPtr display, IntPtr picture, string filter, IntPtr parameters, int nparams);

    [DllImport(Xrender)]
    public static extern void XRenderComposite(IntPtr display, int op, IntPtr src, IntPtr mask, IntPtr dst,
        int srcX, int srcY, int maskX, int maskY, int dstX, int dstY, uint width, uint height);
}
