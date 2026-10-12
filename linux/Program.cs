using System;
using Avalonia;
using ClientOPreview.Linux.Native;
using ClientOPreview.Services;

namespace ClientOPreview.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Before any other Xlib call: the hotkey thread has its own connection, but Xlib still
        // needs its global locks initialised once per process.
        Xlib.XInitThreads();

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            Console.Error.WriteLine("client-o-preview needs an X11 session (DISPLAY is not set). Wayland cannot capture other apps' windows.");
            return 1;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) AppLog.Error("unhandled", ex);
        };

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Error("startup", ex);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    // Software by default: measured with 9 previews on an NVIDIA card, GLX cost ~90% of a core
    // (the driver spins on every window's buffer swap) and a GL context per window costs VRAM.
    // Skia in software paints these small windows for ~8%. COP_RENDER=glx|egl overrides.
    private static X11RenderingMode[] RenderingModes() => Environment.GetEnvironmentVariable("COP_RENDER") switch
    {
        "glx" => new[] { X11RenderingMode.Glx },
        "egl" => new[] { X11RenderingMode.Egl },
        "software" => new[] { X11RenderingMode.Software },
        _ => new[] { X11RenderingMode.Software }
    };

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { EnableIme = false, RenderingMode = RenderingModes() })
            .LogToTrace();
}
