using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

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
            menu.Items.Add("Open", null, (s, e) => Restore(window));
            menu.Items.Add("Exit", null, (s, e) => { _icon!.Visible = false; window.Close(); });
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
        _icon.BalloonTipText = "Minimized to tray.";
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
