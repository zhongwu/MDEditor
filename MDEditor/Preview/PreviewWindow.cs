using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MDEditor.Preview;

public enum PreviewWindowMode
{
    /// <summary>Borderless full-screen preview with larger type; follows editor changes live.</summary>
    Presentation,

    /// <summary>Normal window that renders a snapshot of the document and opens the print preview UI.</summary>
    Print
}

/// <summary>
/// Stand-alone window hosting its own WebView2 for presentation mode and print preview.
/// Kept separate from the main preview pane so it can be full-screen / full-size
/// regardless of the pane's current width or visibility.
/// </summary>
public sealed class PreviewWindow : Window
{
    private const string PresentationScript = @"
(function () {
    function post(key) {
        if (window.chrome && window.chrome.webview)
            window.chrome.webview.postMessage(JSON.stringify({ type: 'key', key: key }));
    }
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { e.preventDefault(); post('Escape'); }
    });
    document.addEventListener('DOMContentLoaded', function () {
        var s = document.createElement('style');
        s.textContent =
            'body{font-size:1.3em;max-width:1100px;padding:48px 64px;}' +
            '.mde-presentation-hint{position:fixed;right:24px;bottom:18px;z-index:2147483647;' +
            'background:rgba(0,0,0,.7);color:#fff;padding:6px 12px;border-radius:4px;' +
            'font:13px \'Segoe UI\',sans-serif;transition:opacity .8s;pointer-events:none;}' +
            '@media print{.mde-presentation-hint{display:none}}';
        document.head.appendChild(s);
        var hint = document.createElement('div');
        hint.className = 'mde-presentation-hint';
        hint.textContent = 'Presentation mode — press Esc to exit';
        document.documentElement.appendChild(hint);
        setTimeout(function () { hint.style.opacity = '0'; }, 2500);
        setTimeout(function () { if (hint.parentNode) hint.parentNode.removeChild(hint); }, 3500);
    });
})();";

    private readonly WebView2 _webView = new();
    private readonly IMarkdownParser _parser;
    private readonly PreviewWindowMode _mode;
    private readonly double _initialScrollPct;

    // Presentation mode mirrors the live app theme/custom CSS; print preview always renders
    // light with no custom CSS regardless of the app's current theme (printing a dark
    // background wastes ink, and most print/PDF engines handle it poorly). Both keep the image
    // base URL so relative images (e.g. in a GitHub file) still resolve.
    private readonly PreviewRenderOptions? _previewOptions;

    private string _markdown;
    private bool _ready;
    private bool _firstNavigationDone;
    private bool _rendering;
    private string? _pendingMarkdown;
    private bool _closing;

    public PreviewWindow(IMarkdownParser parser, string markdown, PreviewWindowMode mode, double initialScrollPct = 0, PreviewRenderOptions? previewOptions = null)
    {
        _parser = parser;
        _markdown = markdown;
        _mode = mode;
        _initialScrollPct = Math.Clamp(initialScrollPct, 0, 1);
        _previewOptions = mode == PreviewWindowMode.Presentation
            ? previewOptions
            : previewOptions?.ImageBaseUrl != null
                ? new PreviewRenderOptions(IsDarkTheme: false, CustomCss: null, previewOptions.Value.ImageBaseUrl, previewOptions.Value.ImageRootUrl)
                : null;

        Background = _previewOptions?.IsDarkTheme == true
            ? new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17))
            : Brushes.White;
        ShowInTaskbar = mode == PreviewWindowMode.Presentation;

        if (mode == PreviewWindowMode.Presentation)
        {
            Title = Loc.Get("Title_Presentation");
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Content = _webView;
        }
        else
        {
            Title = Loc.Get("Title_PrintPreview");
            Width = 1000;
            Height = 800;
            MinWidth = 600;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = BuildPrintLayout();
        }

        Loaded += async (s, e) => await InitializeAsync();
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += (s, e) => _closing = true;
    }

    private UIElement BuildPrintLayout()
    {
        var toolbar = new ToolBar();

        var printButton = new Button { Content = Loc.Get("Print_Print"), ToolTip = Loc.Get("Print_PrintTip"), Margin = new Thickness(2, 0, 2, 0) };
        printButton.Click += (s, e) => ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        toolbar.Items.Add(printButton);

        var systemButton = new Button { Content = Loc.Get("Print_System"), ToolTip = Loc.Get("Print_SystemTip"), Margin = new Thickness(2, 0, 2, 0) };
        systemButton.Click += (s, e) => ShowPrintUI(CoreWebView2PrintDialogKind.System);
        toolbar.Items.Add(systemButton);

        toolbar.Items.Add(new Separator());

        var closeButton = new Button { Content = Loc.Get("Common_Close"), ToolTip = Loc.Get("Print_CloseTip"), Margin = new Thickness(2, 0, 2, 0) };
        closeButton.Click += (s, e) => Close();
        toolbar.Items.Add(closeButton);

        var panel = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);
        panel.Children.Add(_webView);
        return panel;
    }

    private async Task InitializeAsync()
    {
        try
        {
            var environment = await PreviewHost.GetEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            if (_closing) return;

            var core = _webView.CoreWebView2;
            PreviewHost.AttachImageAuthorization(core);
            core.NavigationStarting += (s, e) =>
            {
                if (e.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    e.Cancel = true;
            };
            core.NavigationCompleted += OnNavigationCompleted;
            core.WebMessageReceived += OnWebMessageReceived;

            if (_mode == PreviewWindowMode.Presentation)
                await core.AddScriptToExecuteOnDocumentCreatedAsync(PresentationScript);

            var html = await Task.Run(() => _previewOptions.HasValue
                ? _parser.ToHtmlDocument(_markdown, _previewOptions.Value)
                : _parser.ToHtmlDocument(_markdown));
            if (_closing) return;
            core.NavigateToString(html);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PreviewWindow initialization failed: {ex.Message}");
            MessageBox.Show(this, Loc.Format("Msg_PreviewWindowFailed", ex.Message), "MDEditor",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_firstNavigationDone || _closing) return;
        _firstNavigationDone = true;
        _ready = true;

        try
        {
            if (_initialScrollPct > 0)
            {
                var pct = _initialScrollPct.ToString(CultureInfo.InvariantCulture);
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $"(function(){{var m=document.documentElement.scrollHeight-window.innerHeight;if(m>0)window.scrollTo(0,m*{pct});}})();");
            }

            _webView.Focus();

            if (_mode == PreviewWindowMode.Print)
            {
                // Give KaTeX / Mermaid a moment to finish rendering before the print snapshot.
                await Task.Delay(500);
                if (!_closing)
                    ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
            else if (_pendingMarkdown != null)
            {
                var pending = _pendingMarkdown;
                _pendingMarkdown = null;
                UpdateMarkdown(pending);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PreviewWindow navigation handling failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pushes new editor content into a presentation window (ignored for print preview,
    /// which is a snapshot).
    /// </summary>
    public async void UpdateMarkdown(string markdown)
    {
        if (_mode != PreviewWindowMode.Presentation || _closing)
            return;

        _markdown = markdown;
        if (!_ready || _rendering)
        {
            _pendingMarkdown = markdown;
            return;
        }

        _rendering = true;
        try
        {
            await PreviewHost.RenderAsync(_webView.CoreWebView2, _parser, markdown, _previewOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Presentation update failed: {ex.Message}");
        }
        finally
        {
            _rendering = false;
        }

        if (_pendingMarkdown != null && _pendingMarkdown != markdown)
        {
            var pending = _pendingMarkdown;
            _pendingMarkdown = null;
            UpdateMarkdown(pending);
        }
        else
        {
            _pendingMarkdown = null;
        }
    }

    private void ShowPrintUI(CoreWebView2PrintDialogKind kind)
    {
        try
        {
            _webView.CoreWebView2?.ShowPrintUI(kind);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.Format("Msg_PrintUnavailable", ex.Message), Loc.Get("Shortcut_PrintPreview"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetString() == "key" &&
                root.TryGetProperty("key", out var key) && key.GetString() == "Escape")
            {
                CloseOnce();
            }
        }
        catch
        {
            // ignore malformed messages
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Esc only exits presentation mode: in print preview it belongs to the
        // browser print dialog (closing the dialog, not the window).
        if (_mode == PreviewWindowMode.Presentation && (key == Key.Escape || key == Key.F5))
        {
            e.Handled = true;
            CloseOnce();
        }
        else if (_mode == PreviewWindowMode.Print && key == Key.P &&
                 (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
    }

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        Dispatcher.BeginInvoke(new Action(Close));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _webView.Dispose();
    }
}
