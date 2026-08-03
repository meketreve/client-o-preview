using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ClientOPreview.Localization;

namespace ClientOPreview;

public static class TrayHelper
{
    private static NotifyIcon? _icon;

    public static void Ensure(MainWindow window, bool enabled)
    {
        if (!enabled)
        {
            if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; }
            return;
        }
        if (_icon == null)
        {
            _icon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "client-o-preview"
            };
            _icon.DoubleClick += (s, e) => Restore(window);
            var menu = new ContextMenuStrip();
            var open = menu.Items.Add(Loc.Get("TrayOpen"), null, (s, e) => Restore(window));
            var exit = menu.Items.Add(Loc.Get("TrayExit"), null, (s, e) => { _icon!.Visible = false; window.ForceClose(); });
            Loc.LanguageChanged += (s, e) =>
            {
                open.Text = Loc.Get("TrayOpen");
                exit.Text = Loc.Get("TrayExit");
            };
            _icon.ContextMenuStrip = menu;
        }
        else
        {
            _icon.Visible = true;
        }
    }

    public static void MinimizeToTray(MainWindow window)
    {
        Ensure(window, true);
        window.Hide();
        _icon!.BalloonTipTitle = "client-o-preview";
        _icon.BalloonTipText = Loc.Get("TrayMinimized");
        _icon.ShowBalloonTip(1000);
    }

    private static void Restore(MainWindow window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
        if (_icon != null) _icon.Visible = true;
    }
}
