using System;
using System.IO;
using System.Text;

namespace ClientOPreview.Services;

/// <summary>
/// File diagnostics for everything that used to be swallowed by an empty catch.
/// Writes next to settings.json (%APPDATA%/client-o-preview/) instead of the working
/// directory, which for a single-file exe launched from a shortcut may be unwritable.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    /// <summary>Folder that holds settings.json and error.log.</summary>
    public static string Directory { get; }

    public static string LogPath { get; }

    static AppLog()
    {
        string dir;
        try
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dir = Path.Combine(appdata, "client-o-preview");
            System.IO.Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = AppDomain.CurrentDomain.BaseDirectory;
        }
        Directory = dir;
        LogPath = Path.Combine(dir, "error.log");
    }

    public static void Warn(string context, string message) => Write("WARN", context, message);

    public static void Warn(string context, Exception ex) => Write("WARN", context, ex.ToString());

    public static void Error(string context, Exception ex) => Write("ERROR", context, ex.ToString());

    private static void Write(string level, string context, string message)
    {
        try
        {
            lock (Gate)
            {
                // Keep the log from growing without bound across months of use.
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > MaxBytes) File.Delete(LogPath);

                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {context}: {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never be the thing that crashes the app.
        }
    }
}
