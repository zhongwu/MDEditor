using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using MDEditor.Core.Localization;

namespace MDEditor;

public partial class AboutWindow : Window
{
    private readonly Version? _version = Assembly.GetExecutingAssembly().GetName().Version;

    public AboutWindow()
    {
        InitializeComponent();

        RefreshVersionText();
        Loc.LanguageChanged += OnLanguageChanged;
        Closed += (s, e) => Loc.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshVersionText();

    private void RefreshVersionText()
    {
        VersionText.Text = _version != null ? Loc.Format("About_Version", _version.ToString(3)) : string.Empty;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Best effort - no browser configured or available.
        }
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
