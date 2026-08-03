using System.Reflection;
using System.Windows.Controls;

namespace ClientOPreview.Views;

public partial class AboutPage : System.Windows.Controls.UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        TxtVersion.Text = $"client-o-preview {ReadVersion()}";
    }

    // Comes from <Version> in ClientOPreview.csproj, so it can never drift from the build.
    private static string ReadVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(info))
        {
            int meta = info.IndexOf('+');   // strip the source revision suffix
            return meta > 0 ? info[..meta] : info;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
    }
}
