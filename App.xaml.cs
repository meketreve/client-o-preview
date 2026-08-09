using System;
using System.Windows;
using ClientOPreview.Localization;
using ClientOPreview.Services;

namespace ClientOPreview;

public partial class App : System.Windows.Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            Report(e.Exception, Loc.Get("ErrorUnhandled"));
            e.Handled = true;
        };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var win = new MainWindow();
            win.Show();
        }
        catch (Exception ex)
        {
            Report(ex, Loc.Get("ErrorStartup"));
            Shutdown(-1);
        }
    }

    private static void Report(Exception ex, string caption)
    {
        AppLog.Error(caption, ex);
        // Point at the log instead of leaving the user with a wall of stack trace and no file.
        System.Windows.MessageBox.Show($"{ex}\n\n{AppLog.LogPath}", caption);
    }
}
