using System.Windows;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Localization;
using MDEditor.Core.Models;
using MDEditor.Preview;
using MDEditor.Themes;
using Microsoft.Web.WebView2.Wpf;

namespace MDEditor;

/// <summary>
/// Non-modal, single-instance Markdown syntax reference, rendered through the app's own
/// <see cref="IMarkdownParser"/>/WebView2 pipeline so the examples render exactly like the real
/// preview pane (and follow the app's current theme).
/// </summary>
public sealed class MarkdownGuideWindow : Window
{
    private readonly WebView2 _webView = new();
    private readonly IMarkdownParser _parser;

    public MarkdownGuideWindow(IMarkdownParser parser)
    {
        _parser = parser;

        Title = Loc.Get("Menu_MarkdownGuide");
        Width = 720;
        Height = 760;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ThemeService.IsDark ? new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)) : Brushes.White;
        Content = _webView;

        Loaded += async (s, e) => await InitializeAsync();
        Loc.LanguageChanged += OnLanguageChanged;
        Closed += (s, e) => Loc.LanguageChanged -= OnLanguageChanged;
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = Loc.Get("Menu_MarkdownGuide");
        if (_webView.CoreWebView2 != null)
            await RenderGuideAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var environment = await PreviewHost.GetEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (e.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    e.Cancel = true;
            };

            await RenderGuideAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MarkdownGuideWindow initialization failed: {ex.Message}");
        }
    }

    private async Task RenderGuideAsync()
    {
        var guideMarkdown = Loc.Get("MarkdownGuide_Content");
        var html = await Task.Run(() => _parser.ToHtmlDocument(guideMarkdown, new PreviewRenderOptions(ThemeService.IsDark, CustomCss: null)));
        _webView.CoreWebView2.NavigateToString(html);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _webView.Dispose();
    }
}
