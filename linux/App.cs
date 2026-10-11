using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ClientOPreview.Linux.Platform;
using ClientOPreview.Localization;
using ClientOPreview.Services;

namespace ClientOPreview.Linux;

/// <summary>Code-only Avalonia app: no XAML, so every screen is one .cs file to open.</summary>
public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // After Avalonia set up its own X connection, so ours is the handler that stays.
            X11.InstallErrorHandler();
            var x = X11.Open() ?? throw new System.InvalidOperationException("cannot open X display");

            var service = new SettingsService();
            var settings = service.GetSettings();
            Loc.SetLanguage(string.IsNullOrEmpty(settings.Language) ? Loc.SystemDefault() : settings.Language);

            AppLog.Info("Startup", $"linux build, settings at {service.FilePath}");
            desktop.MainWindow = new MainWindow(x, service);
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
