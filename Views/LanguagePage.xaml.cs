using System;
using System.Windows;
using System.Windows.Controls;
using ClientOPreview.Localization;

namespace ClientOPreview.Views;

public partial class LanguagePage : System.Windows.Controls.UserControl
{
    private bool _loading;

    public event EventHandler<string>? LanguageSelected;

    public LanguagePage()
    {
        InitializeComponent();
        LoadFrom(Loc.CurrentLanguage);
    }

    public void LoadFrom(string? code)
    {
        _loading = true;
        var normalized = Loc.Normalize(code);
        RbPt.IsChecked = normalized == Loc.PortugueseBr;
        RbEn.IsChecked = normalized == Loc.English;
        _loading = false;
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string code)
            LanguageSelected?.Invoke(this, code);
    }
}
