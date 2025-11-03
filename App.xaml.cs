using System.Windows;

namespace ClientOPreview;

public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            try { System.IO.File.WriteAllText("error.log", e.Exception.ToString()); } catch { }
            MessageBox.Show(e.Exception.ToString(), "Unhandled Exception");
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
            try { System.IO.File.WriteAllText("error.log", ex.ToString()); } catch { }
            MessageBox.Show(ex.ToString(), "Startup Error");
            Shutdown(-1);
        }
    }
}
