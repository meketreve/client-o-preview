using System;
using System.Windows.Markup;
// WinForms is enabled in this project, so pin the WPF types.
using Binding = System.Windows.Data.Binding;
using BindingMode = System.Windows.Data.BindingMode;

namespace ClientOPreview.Localization;

/// <summary>
/// {loc:Tr KeyName} — binds a control to the string table so it follows language changes.
/// Kept apart from <see cref="Loc"/> so the table itself stays free of WPF and can be tested.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
