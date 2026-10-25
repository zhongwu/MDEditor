using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Editor;
using MDEditor.Themes;
using MDEditor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MDEditor;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly IMarkdownParser _markdownParser;

    private bool _isDraggingSplitter;
    private double _initialEditorWidth;
    private double _initialMouseX;

    // Line numbers state
    private bool _lineNumbersVisible = false;
    private bool _minimapVisible = true;
    private bool _scrollSyncEnabled = true;
    private System.Windows.Threading.DispatcherTimer? _previewTimer;
    private System.Windows.Threading.DispatcherTimer? _previewScrollTimer;
    private bool _syncingScroll;
    private DateTime _syncCooldownUntil = DateTime.MinValue;
    private string _lastRenderedMarkdown = string.Empty;

    // Zoom (editor font size + preview zoom, driven by the status bar zoom combo)
    private double _baseEditorFontSize = 14.0;
    private const int DefaultZoomIndex = 2; // "100%" item in StatusZoomCombo

    // Preview customization (Settings window wires the real value in; empty until then)
    private string? _customCss;

    // Line ending used when saving (Settings window changes this live)
    private string _lineEnding = Environment.NewLine == "\n" ? "LF" : "CRLF";

    // Keyboard shortcuts rebindable from the Settings window - only the commands that have a real
    // Window.InputBindings entry (zoom in/out also have a fixed, unlisted numpad alias binding that
    // a rebind doesn't touch). The editor's formatting shortcuts live on MarkdownEditor.InputBindings
    // and aren't rebindable.
    private static readonly (string Key, string LabelKey, Func<MainWindowViewModel, ICommand> GetCommand)[] RebindableShortcuts =
    {
        ("newFile", "Title_NewFile", vm => vm.NewFileCommand),
        ("openFile", "Title_OpenFile", vm => vm.OpenFileCommand),
        ("saveFile", "Menu_Save", vm => vm.SaveFileCommand),
        ("saveAsFile", "Shortcut_SaveAs", vm => vm.SaveAsFileCommand),
        ("togglePreview", "Menu_ShowPreview", vm => vm.TogglePreviewCommand),
        ("toggleOutline", "Menu_ShowOutline", vm => vm.ToggleOutlineCommand),
        ("find", "Menu_Find", vm => vm.FindCommand),
        ("replace", "Menu_Replace", vm => vm.ReplaceCommand),
        ("goToLine", "Shortcut_GoToLine", vm => vm.GoToLineCommand),
        ("findInPreview", "Menu_FindInPreview", vm => vm.FindInPreviewCommand),
        ("printPreview", "Shortcut_PrintPreview", vm => vm.PrintPreviewCommand),
        ("presentationMode", "Menu_PresentationMode", vm => vm.PresentationModeCommand),
        ("zoomIn", "Menu_ZoomIn", vm => vm.ZoomInCommand),
        ("zoomOut", "Menu_ZoomOut", vm => vm.ZoomOutCommand),
        ("resetZoom", "Menu_ResetZoom", vm => vm.ResetZoomCommand),
    };

    private readonly Dictionary<string, (ModifierKeys Modifiers, Key Key)> _defaultShortcuts = new();

    // Editor enhancements (Phase 6.1)
    private const int MaxEditorFeatureLength = 1_000_000;
    private readonly CompletionPopup _completionPopup = new();
    private CompletionResult? _completionResult;
    private System.Windows.Threading.DispatcherTimer? _completionTimer;
    private bool _completionSuppressed;
    private bool _smartTypographyEnabled = true;
    private BracketHighlightOverlay? _bracketOverlay;
    private string? _cachedEditorText;
    private MenuItem? _smartTypographyMenuItem;

    // File management state
    private string? _currentFilePath = null;
    private bool _isDirty = false;
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private readonly ISettingsService _settingsService;
    private readonly IGitHubAuthService _gitHubAuthService;
    private readonly IGitHubRepositoryService _gitHubRepoService;
    private readonly IGitHubGistService _gistService;
    private readonly List<string> _recentFiles = new();

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _markdownParser = App.Services.GetRequiredService<IMarkdownParser>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();
        _smartTypographyMenuItem = SmartTypographyMenuItem;

        // Load persisted view settings (Preview state is now managed by ViewModel)
        _lineNumbersVisible = _settingsService.Get<bool>("editor.lineNumbers", false);
        ViewModel.IsPreviewVisible = _settingsService.Get<bool>("editor.preview", true);
        ViewModel.IsEditorVisible = _settingsService.Get<bool>("editor.editorVisible", true);
        _minimapVisible = _settingsService.Get<bool>("editor.minimap", true);
        _scrollSyncEnabled = _settingsService.Get<bool>("editor.scrollSync", true);
        _smartTypographyEnabled = _settingsService.Get<bool>("editor.smartTypography", true);

        // Phase 6.3: settings-driven editor/preview/markdown customization
        _baseEditorFontSize = _settingsService.Get("editor.fontSize", 14.0);
        _customCss = _settingsService.Get<string?>("preview.customCss", null);
        _lineEnding = _settingsService.Get("editor.lineEnding", _lineEnding);
        MarkdownEditor.FontFamily = new FontFamily(_settingsService.Get("editor.fontFamily", "Cascadia Code, Consolas, monospace"));
        StatusLineEnding.Text = _lineEnding;
        _markdownParser.UpdateOptions(new MarkdownParserOptions
        {
            EnableMath = _settingsService.Get("markdown.enableMath", true),
            EnableDiagrams = _settingsService.Get("markdown.enableDiagrams", true),
            EnableEmoji = _settingsService.Get("markdown.enableEmoji", true),
            EnableFootnotes = _settingsService.Get("markdown.enableFootnotes", true),
            EnableTaskLists = _settingsService.Get("markdown.enableTaskLists", true),
        });

        ViewModel.TogglePreviewCallback = TogglePreview;
        ViewModel.InsertMarkdownCallback = InsertMarkdown;
        ViewModel.UndoCallback = () => MarkdownEditor.Undo();
        ViewModel.RedoCallback = () => MarkdownEditor.Redo();
        ViewModel.CutCallback = () => MarkdownEditor.Cut();
        ViewModel.CopyCallback = () => MarkdownEditor.Copy();
        ViewModel.PasteCallback = () => MarkdownEditor.Paste();
        ViewModel.SelectAllCallback = () => MarkdownEditor.SelectAll();
        ViewModel.FindCallback = () => ShowEditorFind(replace: false);
        ViewModel.ReplaceCallback = () => ShowEditorFind(replace: true);
        ViewModel.GoToLineCallback = ShowGoToLine;
        ViewModel.ToggleWordWrapCallback = ToggleWordWrap;
        ViewModel.NewFileCallback = NewFile;
        ViewModel.OpenFileCallback = OpenFile;
        ViewModel.SaveFileCallback = SaveFile;
        ViewModel.SaveAsFileCallback = SaveAsFile;
        ViewModel.ExportHtmlCallback = ExportHtml;
        ViewModel.ExportPdfCallback = ExportPdf;

        // Line numbers toggle
        ViewModel.ToggleLineNumbersCallback = ToggleLineNumbers;

        // Zoom
        ViewModel.ZoomInCallback = ZoomIn;
        ViewModel.ZoomOutCallback = ZoomOut;
        ViewModel.ResetZoomCallback = ResetZoom;

        // Settings
        ViewModel.SettingsCallback = ShowSettingsWindow;

        // Help
        ViewModel.MarkdownGuideCallback = ShowMarkdownGuideWindow;
        ViewModel.AboutCallback = ShowAboutWindow;

        // GitHub (Phase 5)
        ViewModel.GitHubAccountsCallback = ShowGitHubAccountsWindow;
        ViewModel.OpenFromGitHubCallback = OpenFromGitHub;
        ViewModel.GitHubSyncCallback = CommitToGitHub;
        ViewModel.GitHubHistoryCallback = ShowGitHubHistory;
        ViewModel.OpenGistCallback = OpenGist;
        ViewModel.SaveAsGistCallback = SaveAsGist;
        ViewModel.ViewGistCallback = ViewGistOnGitHub;
        ViewModel.GistHistoryCallback = ShowGistHistory;
        _gitHubAuthService = App.Services.GetRequiredService<IGitHubAuthService>();
        _gitHubRepoService = App.Services.GetRequiredService<IGitHubRepositoryService>();
        _gistService = App.Services.GetRequiredService<IGitHubGistService>();
        _gitHubAuthService.AccountsChanged += (s, e) => UpdateGitHubStatus();
        MDEditor.Preview.PreviewHost.ImageAuthorization = GetGitHubImageAuthorization;
        UpdateGitHubStatus();
        _statusWordsTimer.Tick += (s, e) => UpdateWordCountStatus();
        UpdateWordCountStatus();

        // Phase 6.4: XAML text follows {loc:Tr} bindings; text this window sets from code is
        // refreshed here.
        Loc.LanguageChanged += (s, e) =>
        {
            UpdateTitleBar();
            UpdateGitHubStatus();
            UpdateRecentFilesMenu();
            UpdateCursorStatus();
            UpdateWordCountStatus();
        };

        // Phase 6.2: outline panel, find in preview, print preview, presentation mode
        ViewModel.ToggleOutlineCallback = ToggleOutline;
        ViewModel.FindInPreviewCallback = ShowPreviewFind;
        ViewModel.PrintPreviewCallback = ShowPrintPreview;
        ViewModel.PresentationModeCallback = TogglePresentationMode;
        InitializePreviewEnhancements();
        InitializeEditorFind();

        DataContext = ViewModel;

        // Column widths / splitter visibility / menu checkmarks for the editor and preview
        // panes are driven from code-behind rather than XAML DataTriggers, since
        // ColumnDefinition does not reliably participate in DataContext inheritance.
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsEditorVisible) || e.PropertyName == nameof(ViewModel.IsPreviewVisible))
                OnPaneVisibilityChanged();
        };

        // Re-color the title bar and re-render the (themed) preview whenever the theme changes.
        ThemeService.ThemeChanged += OnThemeChanged;

        // KeyBinding.Command bindings only resolve once DataContext is set, so this must run
        // after the assignment above - otherwise every kb.Command would still be null and the
        // lookup-by-command-reference below would silently match nothing.
        InitializeShortcuts();

        StatusZoomCombo.SelectedIndex = _settingsService.Get("editor.zoomIndex", DefaultZoomIndex);

        InitializePreview();
        SetupSplitter();

        // Sync line numbers scroll with editor scroll
        Loaded += (s, e) =>
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += (sender, args) =>
                {
                    // Line numbers and highlights are drawn in screen space, so they have to
                    // be repainted when the viewport moves.
                    if (_lineNumbersVisible)
                        LineNumberGutter.InvalidateVisual();
                    _bracketOverlay?.InvalidateVisual();
                    EditorFindOverlay.InvalidateVisual();
                    GoToLineOverlay.InvalidateVisual();

                    // Keep the preview pane in sync with editor scrolling.
                    SchedulePreviewScrollSync();
                };
            }

            SetupBracketOverlay();
            UpdateBracketHighlight();

            // Minimap overview on the right side of the editor.
            Minimap.Attach(MarkdownEditor);

            // Apply loaded view settings to UI elements
            OnPaneVisibilityChanged();

            LineNumberGutter.Visibility = _lineNumbersVisible ? Visibility.Visible : Visibility.Collapsed;
            LineNumbersMenuItem.IsChecked = _lineNumbersVisible;
            LineNumberGutter.Attach(MarkdownEditor);

            Minimap.Visibility = _minimapVisible ? Visibility.Visible : Visibility.Collapsed;
            MinimapMenuItem.IsChecked = _minimapVisible;

            ScrollSyncMenuItem.IsChecked = _scrollSyncEnabled;
            SmartTypographyMenuItem.IsChecked = _smartTypographyEnabled;

            ApplyOutlineVisibility();
        };

        // Set paragraph style for consistent line height
        var style = new Style(typeof(Paragraph));
        style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Paragraph.LineHeightProperty, 18.0));
        // Without BlockLineHeight a line containing taller fallback glyphs (emoji such as ✅)
        // grows past 18px and the rows become uneven.
        style.Setters.Add(new Setter(Paragraph.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        MarkdownEditor.Resources.Add(typeof(Paragraph), style);

        // Setup preview refresh timer (debounced 1000ms for better performance)
        _previewTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _previewTimer.Tick += (s, e) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
            UpdateOutline();
        };

        // Handle Tab/Shift+Tab for indentation
        MarkdownEditor.PreviewKeyDown += MarkdownEditor_PreviewKeyDown;

        // Phase 6.1: smart typography, completion popup and bracket highlighting
        MarkdownEditor.PreviewTextInput += MarkdownEditor_PreviewTextInput;
        MarkdownEditor.SelectionChanged += (s, e) =>
        {
            UpdateBracketHighlight();
            UpdateCursorStatus();
        };
        MarkdownEditor.LostKeyboardFocus += (s, e) => HideCompletion();

        // The completion popup is sticky so it cannot swallow the first click.
        // Dismiss it on any mouse press that reaches the main window.
        PreviewMouseDown += (s, e) => HideCompletion();
        _completionPopup.ItemAccepted += (s, item) => AcceptCompletion(item);
        SetupCompletionTimer();
        LoadEditorEnhancementSettings();

        // Enable drag & drop on editor
        MarkdownEditor.AllowDrop = true;
        MarkdownEditor.PreviewDragOver += MarkdownEditor_DragOver;
        MarkdownEditor.PreviewDrop += MarkdownEditor_Drop;

        // Disable drag & drop on preview - handled at OS level by WebView2
        MarkdownPreview.AllowDrop = false;

        // Handle editor mouse click to scroll preview to corresponding position
        MarkdownEditor.PreviewMouseLeftButtonUp += (s, e) =>
        {
            // Delay to ensure caret position is updated after click
            Dispatcher.BeginInvoke(new Action(() => SyncPreviewToCaret()), System.Windows.Threading.DispatcherPriority.Input);
        };

        // Load recent files
        LoadRecentFiles();

        // Setup auto-save timer
        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settingsService.Get("editor.autoSaveIntervalSeconds", 30))
        };
        _autoSaveTimer.Tick += (s, e) =>
        {
            AutoSaveDraft();
        };
        _autoSaveTimer.Start();

        // Check for crash recovery
        CheckCrashRecovery();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
    }

    private void SyncPreviewToCaret()
    {
        if (MarkdownPreview.CoreWebView2 == null) return;

        try
        {
            // This scrolls the preview programmatically, which the preview's injected scroll
            // listener reports back via postMessage like any other scroll. Without these guards
            // Preview_WebMessageReceived would treat that as the user scrolling the preview and
            // re-scroll the editor to match it - visibly jumping the editor's own view right
            // after a click. The cooldown is longer than DoPreviewScrollSync's because the
            // heading-match branch below uses a 'smooth' (animated) scroll, which keeps firing
            // scroll events for a bit longer than an instant jump does.
            _syncingScroll = true;
            _syncCooldownUntil = DateTime.Now.AddMilliseconds(600);
            Dispatcher.BeginInvoke(new Action(() => _syncingScroll = false), System.Windows.Threading.DispatcherPriority.Input);

            var fullText = GetEditorText();
            var caretRange = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.CaretPosition);
            var caretText = caretRange.Text;

            string? targetHeading = null;
            var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var caretLineIndex = caretText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length - 1;

            for (int i = caretLineIndex; i >= 0; i--)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("#"))
                {
                    targetHeading = line.TrimStart('#').Trim();
                    break;
                }
            }

            if (!string.IsNullOrEmpty(targetHeading))
            {
                var escapedHeading = targetHeading
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\"", "\\\"");

                var script = $@"
                    (function() {{
                        var headings = document.querySelectorAll('h1, h2, h3, h4, h5, h6');
                        for (var i = 0; i < headings.length; i++) {{
                            if (headings[i].textContent.trim() === '{escapedHeading}') {{
                                headings[i].scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                                return;
                            }}
                        }}
                        var maxScroll = document.documentElement.scrollHeight - window.innerHeight;
                        if (maxScroll < 0) maxScroll = 0;
                        var totalLines = {lines.Length};
                        var caretLine = {caretLineIndex};
                        var pct = totalLines > 1 ? caretLine / (totalLines - 1) : 0;
                        window.scrollTo(0, maxScroll * pct);
                    }})();
                ";
                MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);
            }
            else
            {
                var totalLines = lines.Length;
                var caretLine = caretLineIndex;
                var pct = totalLines > 1 ? (double)caretLine / (totalLines - 1) : 0;
                var script = $@"
                    (function() {{
                        var maxScroll = document.documentElement.scrollHeight - window.innerHeight;
                        if (maxScroll < 0) maxScroll = 0;
                        window.scrollTo(0, maxScroll * {pct.ToString(System.Globalization.CultureInfo.InvariantCulture)});
                    }})();
                ";
                MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncPreviewToCaret error: {ex.Message}");
        }
    }

    private void SchedulePreviewScrollSync()
    {
        if (!_scrollSyncEnabled || _syncingScroll) return;
        if (DateTime.Now < _syncCooldownUntil) return;

        if (_previewScrollTimer == null)
        {
            _previewScrollTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            _previewScrollTimer.Tick += (s, e) =>
            {
                _previewScrollTimer?.Stop();
                DoPreviewScrollSync();
            };
        }

        _previewScrollTimer.Stop();
        _previewScrollTimer.Start();
    }

    private void DoPreviewScrollSync()
    {
        if (!_scrollSyncEnabled || _syncingScroll) return;
        if (DateTime.Now < _syncCooldownUntil) return;
        if (MarkdownPreview.CoreWebView2 == null) return;

        var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
        if (scrollViewer == null || scrollViewer.ExtentHeight <= 0) return;

        double maxScroll = scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
        if (maxScroll < 0) maxScroll = 0;
        double pct = maxScroll > 0 ? scrollViewer.VerticalOffset / maxScroll : 0;
        pct = Math.Clamp(pct, 0, 1);

        _syncingScroll = true;
        _syncCooldownUntil = DateTime.Now.AddMilliseconds(300);

        var script = $"(function(){{var m=document.documentElement.scrollHeight-window.innerHeight;if(m<0)m=0;window.scrollTo(0,m*{pct.ToString(System.Globalization.CultureInfo.InvariantCulture)});}})();";
        MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);

        Dispatcher.BeginInvoke(new Action(() => _syncingScroll = false), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Preview_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_scrollSyncEnabled) return;
        if (DateTime.Now < _syncCooldownUntil) return;

        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json) || !json.Contains("\"scroll\"")) return;

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("type").GetString() != "scroll") return;

            double pct = doc.RootElement.GetProperty("pct").GetDouble();
            pct = Math.Clamp(pct, 0, 1);

            _syncingScroll = true;
            _syncCooldownUntil = DateTime.Now.AddMilliseconds(300);

            var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
            if (scrollViewer != null && scrollViewer.ExtentHeight > 0)
            {
                double maxScroll = scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
                if (maxScroll < 0) maxScroll = 0;
                scrollViewer.ScrollToVerticalOffset(maxScroll * pct);
            }

            Dispatcher.BeginInvoke(new Action(() => _syncingScroll = false), System.Windows.Threading.DispatcherPriority.Input);
        }
        catch
        {
            // ignore malformed messages
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private void MarkdownEditor_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".md" || ext == ".markdown" || ext == ".txt" ||
                    ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".svg")
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void MarkdownEditor_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0)
            return;

        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".md" || ext == ".markdown" || ext == ".txt")
            {
                if (_isDirty)
                {
                    var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("Title_OpenDroppedFile"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                try
                {
                    var encoding = DetectFileEncoding(file);
                    var content = System.IO.File.ReadAllText(file, encoding);
                    _currentFilePath = file;
                    _gitHubDocument = null;
                    _gistDocument = null;
                    SetEditorText(content);
                    AddRecentFile(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Loc.Format("Msg_ErrorOpeningFile", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".svg")
            {
                EmbedDroppedImage(file);
            }
        }
    }

    private void EmbedDroppedImage(string imagePath)
    {
        try
        {
            string assetsFolder;
            string relativePath;

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                var fileDir = System.IO.Path.GetDirectoryName(_currentFilePath)!;
                assetsFolder = System.IO.Path.Combine(fileDir, "assets");
                System.IO.Directory.CreateDirectory(assetsFolder);

                var fileName = System.IO.Path.GetFileName(imagePath);
                var destPath = System.IO.Path.Combine(assetsFolder, fileName);

                if (System.IO.File.Exists(destPath))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    var ext = System.IO.Path.GetExtension(fileName);
                    int counter = 1;
                    while (System.IO.File.Exists(destPath))
                    {
                        destPath = System.IO.Path.Combine(assetsFolder, $"{name}_{counter}{ext}");
                        counter++;
                    }
                }

                System.IO.File.Copy(imagePath, destPath, true);
                relativePath = $"assets/{System.IO.Path.GetFileName(destPath)}";
            }
            else
            {
                relativePath = imagePath.Replace("\\", "/");
            }

            var altText = System.IO.Path.GetFileNameWithoutExtension(imagePath);
            var imageMarkdown = $"![{altText}]({relativePath})";
            InsertMarkdown(imageMarkdown, "");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.Format("Msg_ErrorEmbeddingImage", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MarkdownEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftShift &&
            e.Key != Key.RightShift && e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
        {
            _completionSuppressed = false;
        }

        if (_completionPopup.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    _completionPopup.MoveSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    _completionPopup.MoveSelection(-1);
                    e.Handled = true;
                    return;
                case Key.PageDown:
                    _completionPopup.MoveSelection(6);
                    e.Handled = true;
                    return;
                case Key.PageUp:
                    _completionPopup.MoveSelection(-6);
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    if (_completionPopup.SelectedItem is { } selected)
                    {
                        AcceptCompletion(selected);
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Escape:
                    HideCompletion();
                    _completionSuppressed = true;
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            UpdateCompletions(CompletionTrigger.Manual);
            e.Handled = true;
            return;
        }

        if (_smartTypographyEnabled &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0 &&
            MarkdownEditor.Selection.IsEmpty &&
            TryGetSmartTypographyChar(e, out char smartChar) &&
            GetFeatureText() is { } featureText)
        {
            int caretOffset = EditorText.GetOffset(MarkdownEditor.Document, MarkdownEditor.CaretPosition);
            if (SmartTypography.TryGetReplacement(featureText, caretOffset, smartChar, out string insert, out int removeBefore))
            {
                e.Handled = true;

                if (removeBefore > 0)
                {
                    var start = MarkdownEditor.CaretPosition
                        .GetPositionAtOffset(-removeBefore, LogicalDirection.Backward);
                    if (start != null)
                        MarkdownEditor.Selection.Select(start, MarkdownEditor.CaretPosition);
                }

                MarkdownEditor.Selection.Text = insert;

                var newCaret = MarkdownEditor.CaretPosition
                    .GetPositionAtOffset(insert.Length, LogicalDirection.Forward);
                if (newCaret != null)
                    MarkdownEditor.CaretPosition = newCaret;

                return;
            }
        }

        if (e.Key != Key.Tab)
            return;

        e.Handled = true;
        const string indent = "    ";

        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (isShift)
        {
            if (MarkdownEditor.Selection.IsEmpty)
            {
                var caret = MarkdownEditor.CaretPosition;
                var lineStart = caret.GetLineStartPosition(0);
                if (lineStart != null)
                {
                    var lineEnd = caret.GetLineStartPosition(1) ?? MarkdownEditor.Document.ContentEnd;
                    var lineRange = new TextRange(lineStart, lineEnd);
                    var lineText = lineRange.Text;
                    if (lineText.StartsWith(indent))
                    {
                        lineRange.Text = lineText.Substring(indent.Length);
                    }
                }
            }
            else
            {
                var selectedText = MarkdownEditor.Selection.Text;
                var lines = selectedText.Split('\n');
                var unindented = string.Join("\n", Array.ConvertAll(lines, l =>
                    l.StartsWith(indent) ? l.Substring(indent.Length) : l));
                MarkdownEditor.Selection.Text = unindented;
            }
        }
        else
        {
            if (MarkdownEditor.Selection.IsEmpty)
            {
                MarkdownEditor.Selection.Text = indent;
                var newPos = MarkdownEditor.CaretPosition.GetPositionAtOffset(indent.Length, LogicalDirection.Forward);
                if (newPos != null)
                    MarkdownEditor.CaretPosition = newPos;
            }
            else
            {
                var selectedText = MarkdownEditor.Selection.Text;
                var lines = selectedText.Split('\n');
                var indented = string.Join("\n", Array.ConvertAll(lines, l => indent + l));
                MarkdownEditor.Selection.Text = indented;
            }
        }
    }

    private static bool TryGetSmartTypographyChar(KeyEventArgs e, out char typed)
    {
        typed = '\0';
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        switch (e.Key)
        {
            case Key.OemQuotes:
                typed = shift ? '"' : '\'';
                return true;
            case Key.OemMinus when !shift:
                typed = '-';
                return true;
            default:
                return false;
        }
    }

    private void SetupCompletionTimer()
    {
        _completionTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };

        _completionTimer.Tick += (s, e) =>
        {
            _completionTimer.Stop();
            UpdateCompletions(CompletionTrigger.Typing);
        };
    }

    private void LoadEditorEnhancementSettings()
    {
        _smartTypographyEnabled = _settingsService.Get("editor.smartTypography", true);

        if (_smartTypographyMenuItem != null)
            _smartTypographyMenuItem.IsChecked = _smartTypographyEnabled;
    }

    private void SmartTypography_Click(object sender, RoutedEventArgs e)
    {
        _smartTypographyEnabled = sender is MenuItem item && item.IsChecked;
        _settingsService.Set("editor.smartTypography", _smartTypographyEnabled);
        _settingsService.Save();
    }

    private void MarkdownEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _completionSuppressed = false;
        ScheduleCompletionUpdate();
    }

    private void ScheduleCompletionUpdate()
    {
        if (_completionSuppressed || _completionTimer == null)
            return;

        _completionTimer.Stop();
        _completionTimer.Start();
    }

    private void UpdateCompletions(CompletionTrigger trigger)
    {
        if (!MarkdownEditor.IsKeyboardFocusWithin)
        {
            HideCompletion();
            return;
        }

        var text = GetFeatureText();
        if (text == null || text.Length > MaxEditorFeatureLength)
        {
            HideCompletion();
            return;
        }

        int caret = EditorText.GetOffset(MarkdownEditor.Document, MarkdownEditor.CaretPosition);
        var result = MarkdownCompletionProvider.GetCompletions(text, caret, _currentFilePath, trigger);

        if (result == null || result.Items.Count == 0)
        {
            HideCompletion();
            return;
        }

        _completionResult = result;
        var caretRect = MarkdownEditor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
        _completionPopup.Show(result.Items, MarkdownEditor, caretRect);
    }

    private void AcceptCompletion(CompletionItem item)
    {
        var result = _completionResult;
        _completionResult = null;
        HideCompletion();

        if (result == null)
            return;

        var document = MarkdownEditor.Document;
        var start = EditorText.GetPointer(document, result.TokenStart);
        var end = EditorText.GetPointer(document, result.TokenEnd);
        if (start == null || end == null)
            return;

        MarkdownEditor.Selection.Select(start, end);
        MarkdownEditor.Selection.Text = item.Insert;

        int after = item.Insert.Length - item.CaretOffsetInInsert;
        if (after > 0)
        {
            var target = MarkdownEditor.CaretPosition.GetPositionAtOffset(-after, LogicalDirection.Backward);
            if (target != null)
                MarkdownEditor.CaretPosition = target;
        }

        _completionSuppressed = true;
    }

    private void HideCompletion()
    {
        _completionResult = null;
        if (_completionPopup.IsOpen)
            _completionPopup.Hide();
    }

    private string? GetFeatureText()
    {
        _cachedEditorText ??= EditorText.GetText(MarkdownEditor.Document);
        return _cachedEditorText.Length > MaxEditorFeatureLength ? null : _cachedEditorText;
    }

    private void SetupBracketOverlay()
    {
        _bracketOverlay = BracketOverlay;
        _bracketOverlay.Editor = MarkdownEditor;
    }

    private void UpdateBracketHighlight()
    {
        if (_bracketOverlay == null || !MarkdownEditor.Selection.IsEmpty)
        {
            _bracketOverlay?.SetPair(null, null);
            return;
        }

        var text = GetFeatureText();
        if (text == null)
        {
            _bracketOverlay.SetPair(null, null);
            return;
        }

        var document = MarkdownEditor.Document;
        int caret = EditorText.GetOffset(document, MarkdownEditor.CaretPosition);

        if (BracketMatcher.TryFindMatch(text, caret, out int open, out int close))
        {
            _bracketOverlay.SetPair(
                EditorText.GetPointer(document, open),
                EditorText.GetPointer(document, close));
        }
        else
        {
            _bracketOverlay.SetPair(null, null);
        }
    }

    private Task? _previewReady;

    private Task EnsurePreviewAsync()
    {
        return _previewReady ??= InitializePreviewCoreAsync();
    }

    private async Task InitializePreviewCoreAsync()
    {
        // Shared with the presentation / print preview windows.
        var env = await MDEditor.Preview.PreviewHost.GetEnvironmentAsync();
        await MarkdownPreview.EnsureCoreWebView2Async(env);

        MarkdownPreview.CoreWebView2.WebMessageReceived += Preview_WebMessageReceived;
        MarkdownPreview.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            RestorePreviewFind();
            _ = ApplyZoomAsync(GetCurrentZoomFactor());
        };
        await MarkdownPreview.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            "window.addEventListener('scroll',function(){var m=document.documentElement.scrollHeight-window.innerHeight;if(m<0)m=0;var p=m>0?window.scrollY/m:0;if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage(JSON.stringify({type:'scroll',pct:p}));}}, {passive:true});");
        await MarkdownPreview.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(MDEditor.Preview.PreviewHost.FindScript);
        MDEditor.Preview.PreviewHost.AttachImageAuthorization(MarkdownPreview.CoreWebView2);
    }

    private async void InitializePreview()
    {
        try
        {
            await EnsurePreviewAsync();
            var html = _markdownParser.ToHtmlDocument(GetEditorText(), GetPreviewRenderOptions());
            MarkdownPreview.NavigateToString(html);
            _lastRenderedMarkdown = GetEditorText();

            PreviewMenuItem.IsChecked = ViewModel.IsPreviewVisible;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
        }
    }

    private PreviewRenderOptions GetPreviewRenderOptions() =>
        new(ThemeService.IsDark, _customCss, _gitHubDocument?.RawFolderUrl, _gitHubDocument?.RawRootUrl);

    // --- Settings window hooks: these apply a change live; the Settings window itself persists
    // the value via ISettingsService before calling in. ---

    internal void SetBaseEditorFontSize(double size)
    {
        _baseEditorFontSize = size;
        _ = ApplyZoomAsync(GetCurrentZoomFactor());
    }

    internal void SetEditorFontFamily(string family)
    {
        MarkdownEditor.FontFamily = new FontFamily(family);
    }

    internal void SetAutoSaveInterval(int seconds)
    {
        if (_autoSaveTimer != null)
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    internal void SetLineEnding(string ending)
    {
        _lineEnding = ending;
        StatusLineEnding.Text = ending;
    }

    internal void SetCustomCss(string? css)
    {
        _customCss = css;
        ReloadPreviewFull();
    }

    internal void SetMarkdownOptions(MarkdownParserOptions options)
    {
        _markdownParser.UpdateOptions(options);
        ReloadPreviewFull();
    }

    private void InitializeShortcuts()
    {
        foreach (var (key, _, getCommand) in RebindableShortcuts)
        {
            var binding = FindShortcutBinding(key, getCommand);
            if (binding == null) continue;

            _defaultShortcuts[key] = (binding.Modifiers, binding.Key);

            var overrideGesture = _settingsService.Get<string?>($"shortcuts.overrides.{key}", null);
            if (overrideGesture != null && TryParseGesture(overrideGesture, out var modifiers, out var parsedKey))
            {
                binding.Modifiers = modifiers;
                binding.Key = parsedKey;
            }
        }
    }

    private KeyBinding? FindShortcutBinding(string key, Func<MainWindowViewModel, ICommand> getCommand)
    {
        var command = getCommand(ViewModel);
        return InputBindings.OfType<KeyBinding>().FirstOrDefault(kb => kb.Command == command);
    }

    private static string FormatGesture(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static bool TryParseGesture(string gesture, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i] switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Shift" => ModifierKeys.Shift,
                "Alt" => ModifierKeys.Alt,
                "Win" => ModifierKeys.Windows,
                _ => ModifierKeys.None,
            };
        }

        return Enum.TryParse(parts[^1], out key);
    }

    /// <summary>Rows for the Settings window's Keyboard Shortcuts tab: key, display label, current gesture text.</summary>
    internal IReadOnlyList<(string Key, string Label, string Gesture)> GetShortcutRows() =>
        RebindableShortcuts
            .Select(s =>
            {
                var binding = FindShortcutBinding(s.Key, s.GetCommand);
                var gesture = binding != null ? FormatGesture(binding.Modifiers, binding.Key) : Loc.Get("Shortcut_Unbound");
                return (s.Key, Loc.Get(s.LabelKey), gesture);
            })
            .ToList();

    /// <summary>Checks the proposed gesture against the other 9 tracked shortcuts (not all 53 commands).</summary>
    internal string? FindShortcutConflict(string key, ModifierKeys modifiers, Key newKey)
    {
        foreach (var (otherKey, labelKey, getCommand) in RebindableShortcuts)
        {
            if (otherKey == key) continue;
            var binding = FindShortcutBinding(otherKey, getCommand);
            if (binding != null && binding.Modifiers == modifiers && binding.Key == newKey)
                return Loc.Get(labelKey);
        }
        return null;
    }

    internal void SetShortcut(string key, ModifierKeys modifiers, Key newKey)
    {
        var entry = RebindableShortcuts.FirstOrDefault(s => s.Key == key);
        var binding = FindShortcutBinding(key, entry.GetCommand);
        if (binding == null) return;

        binding.Modifiers = modifiers;
        binding.Key = newKey;
        _settingsService.Set($"shortcuts.overrides.{key}", FormatGesture(modifiers, newKey));
    }

    internal void ResetShortcut(string key)
    {
        if (!_defaultShortcuts.TryGetValue(key, out var original)) return;

        var entry = RebindableShortcuts.FirstOrDefault(s => s.Key == key);
        var binding = FindShortcutBinding(key, entry.GetCommand);
        if (binding == null) return;

        binding.Modifiers = original.Modifiers;
        binding.Key = original.Key;
        _settingsService.Remove($"shortcuts.overrides.{key}");
    }

    private SettingsWindow? _settingsWindow;

    internal void ShowSettingsWindow()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(this, _settingsService, _markdownParser) { Owner = this };
        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private MarkdownGuideWindow? _markdownGuideWindow;

    internal void ShowMarkdownGuideWindow()
    {
        if (_markdownGuideWindow != null)
        {
            _markdownGuideWindow.Activate();
            return;
        }

        _markdownGuideWindow = new MarkdownGuideWindow(_markdownParser) { Owner = this };
        _markdownGuideWindow.Closed += (s, e) => _markdownGuideWindow = null;
        _markdownGuideWindow.Show();
    }

    private AboutWindow? _aboutWindow;

    internal void ShowAboutWindow()
    {
        if (_aboutWindow != null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new AboutWindow { Owner = this };
        _aboutWindow.Closed += (s, e) => _aboutWindow = null;
        _aboutWindow.Show();
    }

    private void OnThemeChanged(bool isDark)
    {
        ThemeService.ApplyTitleBar(this, isDark);
        ReloadPreviewFull();
    }

    /// <summary>
    /// Forces a full preview re-navigation (bypassing PreviewHost's body-swap fast path, which
    /// never touches &lt;head&gt;/&lt;style&gt;) so theme, custom-CSS, and markdown-flavor changes
    /// actually take effect in the live preview.
    /// </summary>
    private async void ReloadPreviewFull()
    {
        try
        {
            await EnsurePreviewAsync();
            var html = _markdownParser.ToHtmlDocument(GetEditorText(), GetPreviewRenderOptions());
            MarkdownPreview.NavigateToString(html);
            _lastRenderedMarkdown = GetEditorText();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReloadPreviewFull failed: {ex.Message}");
        }
    }

    private void SetupSplitter()
    {
        SplitterColumn.PreviewMouseLeftButtonDown += SplitterColumn_MouseLeftButtonDown;
        SplitterColumn.PreviewMouseMove += SplitterColumn_MouseMove;
        SplitterColumn.PreviewMouseLeftButtonUp += SplitterColumn_MouseLeftButtonUp;
    }

    private void SplitterColumn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSplitter = true;
        _initialEditorWidth = EditorColumn.ActualWidth;
        _initialMouseX = e.GetPosition(SplitterColumn).X;
        SplitterColumn.CaptureMouse();
    }

    private void SplitterColumn_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        var currentX = e.GetPosition(SplitterColumn).X;
        var delta = currentX - _initialMouseX;
        var newWidth = _initialEditorWidth + delta;

        var maxWidth = MainContentGrid.ActualWidth - OutlineColumn.ActualWidth - 300 - SplitterColumn.ActualWidth;
        newWidth = Math.Clamp(newWidth, 300, Math.Max(300, maxWidth));
        EditorColumn.Width = new GridLength(newWidth);
    }

    private void SplitterColumn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        _isDraggingSplitter = false;
        SplitterColumn.ReleaseMouseCapture();
    }

    // Word count walks the whole document, so it trails typing by a moment.
    private readonly System.Windows.Threading.DispatcherTimer _statusWordsTimer = CreateStatusWordsTimer();

    private static System.Windows.Threading.DispatcherTimer CreateStatusWordsTimer() =>
        new() { Interval = TimeSpan.FromMilliseconds(250) };

    private void UpdateCursorStatus()
    {
        var before = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.CaretPosition).Text;
        var (line, column) = MDEditor.Core.Services.TextStatistics.GetLineColumn(before);
        StatusCursorPosition.Text = Loc.Format("Status_Position", line, column);
    }

    private void UpdateWordCountStatus()
    {
        _statusWordsTimer.Stop();
        StatusWordCount.Text = Loc.Format("Status_Words", MDEditor.Core.Services.TextStatistics.CountWords(GetEditorText()));
    }

    private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        MarkDirty();

        _cachedEditorText = null;
        UpdateBracketHighlight();
        _statusWordsTimer.Stop();
        _statusWordsTimer.Start();

        _previewTimer?.Stop();
        _previewTimer?.Start();
    }

    private async void UpdatePreview()
    {
        try
        {
            if (MarkdownPreview.CoreWebView2 == null)
            {
                await EnsurePreviewAsync();
                var initMarkdown = GetEditorText();
                var initHtml = await Task.Run(() => _markdownParser.ToHtmlDocument(initMarkdown, GetPreviewRenderOptions()));
                MarkdownPreview.NavigateToString(initHtml);
                _lastRenderedMarkdown = initMarkdown;
                return;
            }

            var markdown = GetEditorText();
            if (markdown == _lastRenderedMarkdown)
                return;

            _lastRenderedMarkdown = markdown;

            // Keep an open presentation window in step with the editor.
            _presentationWindow?.UpdateMarkdown(markdown);

            // Full navigations re-apply the find highlights from NavigationCompleted;
            // in-place body swaps drop them, so re-apply here.
            var navigated = await MDEditor.Preview.PreviewHost.RenderAsync(MarkdownPreview.CoreWebView2, _markdownParser, markdown, GetPreviewRenderOptions());
            if (!navigated)
                RestorePreviewFind();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview update failed: {ex.Message}");
        }
    }

    private string GetEditorText()
    {
        var range = new System.Windows.Documents.TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        return range.Text;
    }

    private void CopyAsMarkdown_Click(object sender, RoutedEventArgs e)
    {
        var text = GetEditorText();
        Clipboard.SetText(text);
    }

    private void CopyAsHtml_Click(object sender, RoutedEventArgs e)
    {
        var markdown = GetEditorText();
        var html = _markdownParser.ToHtmlDocument(markdown);
        Clipboard.SetText(html);
    }

    private System.Text.Encoding DetectFileEncoding(string filePath)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(filePath);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return System.Text.Encoding.UTF8;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return System.Text.Encoding.Unicode;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return System.Text.Encoding.BigEndianUnicode;

            try
            {
                var utf8 = new System.Text.UTF8Encoding(false, true);
                utf8.GetString(bytes);
                return System.Text.Encoding.UTF8;
            }
            catch
            {
                return System.Text.Encoding.Default;
            }
        }
        catch
        {
            return System.Text.Encoding.UTF8;
        }
    }

    private static string UntitledFileName => Loc.Get("Doc_UntitledBase") + ".md";

    /// <summary>File name of the open document, whether local, from a repository, or from a gist.</summary>
    private string CurrentDocumentName =>
        _gitHubDocument?.FileName
        ?? _gistDocument?.FileName
        ?? (string.IsNullOrEmpty(_currentFilePath) ? UntitledFileName : System.IO.Path.GetFileName(_currentFilePath));

    private void UpdateTitleBar()
    {
        var fileName = CurrentDocumentName;
        var location = _gitHubDocument != null ? $" ({_gitHubDocument.RepoFullName}@{_gitHubDocument.Branch})"
            : _gistDocument != null ? $" (Gist: {_gistDocument.DisplayName})"
            : "";
        var dirty = _isDirty ? " ●" : "";
        Title = $"MDEditor - {fileName}{location}{dirty}";
        EditorFileName.Text = fileName + location;
        EditorFileName.ToolTip = _gitHubDocument?.DisplayName ?? _gistDocument?.HtmlUrl ?? _currentFilePath;
        EditorDirtyIndicator.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;

        // Every change of document identity (open, Save As, commit) ends here, so this keeps the
        // GitHub/gist-only menu items in sync.
        ViewModel.IsGitHubDocument = _gitHubDocument != null;
        ViewModel.IsGistDocument = _gistDocument != null;
    }

    private void SetEditorText(string text)
    {
        var range = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        range.Text = text;
        _isDirty = false;
        UpdateTitleBar();
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitleBar();
        }
    }

    private void NewFile()
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("Title_NewFile"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _currentFilePath = null;
        _gitHubDocument = null;
        _gistDocument = null;
        SetEditorText("");
    }

    private void OpenFile()
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("Title_OpenFile"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Dialog_OpenMarkdown"),
            Filter = Loc.Get("Filter_Open"),
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var encoding = DetectFileEncoding(dialog.FileName);
                var content = System.IO.File.ReadAllText(dialog.FileName, encoding);
                _currentFilePath = dialog.FileName;
                _gitHubDocument = null;
                _gistDocument = null;
                SetEditorText(content);
                AddRecentFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Format("Msg_ErrorOpeningFile", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile()
    {
        if (_gitHubDocument != null)
        {
            CommitToGitHub();
            return;
        }

        if (_gistDocument != null)
        {
            SaveGist();
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsFile();
            return;
        }

        try
        {
            var text = MDEditor.Core.Services.LineEndingNormalizer.Normalize(GetEditorText(), _lineEnding);
            System.IO.File.WriteAllText(_currentFilePath, text);
            _isDirty = false;
            if (!string.IsNullOrEmpty(_currentFilePath))
                AddRecentFile(_currentFilePath);
            UpdateTitleBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.Format("Msg_ErrorSavingFile", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsFile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Get("Dialog_SaveMarkdownAs"),
            Filter = Loc.Get("Filter_Save"),
            DefaultExt = ".md",
            FileName = CurrentDocumentName
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var text = MDEditor.Core.Services.LineEndingNormalizer.Normalize(GetEditorText(), _lineEnding);
                System.IO.File.WriteAllText(dialog.FileName, text);
                _currentFilePath = dialog.FileName;
                _gitHubDocument = null;
                _gistDocument = null;
                _isDirty = false;
                AddRecentFile(dialog.FileName);
                UpdateTitleBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Format("Msg_ErrorSavingFile", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportHtml()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Get("Dialog_ExportHtml"),
            Filter = Loc.Get("Filter_Html"),
            DefaultExt = ".html",
            FileName = string.IsNullOrEmpty(_currentFilePath)
                ? Loc.Get("Doc_UntitledBase") + ".html"
                : System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(_currentFilePath), ".html")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var markdown = GetEditorText();
                var html = _markdownParser.ToHtmlDocument(markdown);
                System.IO.File.WriteAllText(dialog.FileName, html);
                MessageBox.Show(Loc.Format("Msg_Exported", dialog.FileName), Loc.Get("Title_ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Format("Msg_ErrorExporting", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }


    private async void ExportPdf()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Get("Dialog_ExportPdf"),
            Filter = Loc.Get("Filter_Pdf"),
            DefaultExt = ".pdf",
            FileName = string.IsNullOrEmpty(_currentFilePath)
                ? Loc.Get("Doc_UntitledBase") + ".pdf"
                : System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(_currentFilePath), ".pdf")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                if (MarkdownPreview.CoreWebView2 != null)
                {
                    bool success = await MarkdownPreview.CoreWebView2.PrintToPdfAsync(dialog.FileName);
                    if (success)
                    {
                        MessageBox.Show(Loc.Format("Msg_Exported", dialog.FileName), Loc.Get("Title_ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(Loc.Get("Msg_PdfUnsupported"), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show(Loc.Get("Msg_PreviewNotReady"), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Format("Msg_PdfFailed", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void InsertMarkdown(string before, string after)
    {
        try
        {
            var selection = MarkdownEditor.Selection;
            var selectedText = selection.Text;
            selection.Text = before + selectedText + after;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InsertMarkdown error: {ex.Message}");
        }
    }

    private void TogglePreview()
    {
        // Do NOT flip the ViewModel boolean here! The ViewModel command already flipped it.
        // The ViewModel property setter also already saved it to settings.
        // OnPaneVisibilityChanged (wired to ViewModel.PropertyChanged) handles the rest.
    }

    /// <summary>
    /// Keeps the editor/preview column widths, splitter visibility, and View-menu checkmarks
    /// in sync with ViewModel.IsEditorVisible / IsPreviewVisible. Deliberately avoids XAML
    /// DataTriggers on ColumnDefinition (which don't reliably pick up DataContext) and on
    /// MenuItem.IsChecked (which conflicts with MenuItem's own IsCheckable click handling) -
    /// both silently failed to update, leaving stale widths/checkmarks.
    /// </summary>
    private void OnPaneVisibilityChanged()
    {
        bool editorVisible = ViewModel.IsEditorVisible;
        bool previewVisible = ViewModel.IsPreviewVisible;

        PreviewMenuItem.IsChecked = previewVisible;
        EditorMenuItem.IsChecked = editorVisible;

        if (editorVisible && previewVisible)
        {
            if (EditorColumn.Width.Value == 0)
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            if (PreviewColumn.Width.Value == 0)
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            EditorColumn.MinWidth = 300;
            PreviewColumn.MinWidth = 300;
            SplitterColumn.Visibility = Visibility.Visible;
        }
        else if (editorVisible)
        {
            if (EditorColumn.Width.Value == 0)
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            EditorColumn.MinWidth = 300;
            PreviewColumn.Width = new GridLength(0);
            PreviewColumn.MinWidth = 0;
            SplitterColumn.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (PreviewColumn.Width.Value == 0)
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            PreviewColumn.MinWidth = 300;
            EditorColumn.Width = new GridLength(0);
            EditorColumn.MinWidth = 0;
            SplitterColumn.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleWordWrap()
    {
        if (MarkdownEditor.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
        {
            MarkdownEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else
        {
            MarkdownEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    private void ToggleLineNumbers()
    {
        _lineNumbersVisible = !_lineNumbersVisible;
        LineNumberGutter.Visibility = _lineNumbersVisible ? Visibility.Visible : Visibility.Collapsed;
        LineNumbersMenuItem.IsChecked = _lineNumbersVisible;
        _settingsService.Set("editor.lineNumbers", _lineNumbersVisible);
        if (_lineNumbersVisible)
        {
            LineNumberGutter.Refresh();
        }
    }

    private void ToggleMinimap_Click(object sender, RoutedEventArgs e)
    {
        _minimapVisible = !_minimapVisible;
        Minimap.Visibility = _minimapVisible ? Visibility.Visible : Visibility.Collapsed;
        MinimapMenuItem.IsChecked = _minimapVisible;
        _settingsService.Set("editor.minimap", _minimapVisible);
    }

    private void ToggleScrollSync_Click(object sender, RoutedEventArgs e)
    {
        _scrollSyncEnabled = !_scrollSyncEnabled;
        ScrollSyncMenuItem.IsChecked = _scrollSyncEnabled;
        _settingsService.Set("editor.scrollSync", _scrollSyncEnabled);
    }

    private TextPointer? GetTextPointerAtOffset(int offset)
    {
        if (offset <= 0)
            return MarkdownEditor.Document.ContentStart;

        var current = MarkdownEditor.Document.ContentStart;
        int count = 0;

        while (count < offset && current != null)
        {
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next == current)
                break;
            current = next;
            count++;
        }

        return current;
    }

    private void LoadRecentFiles()
    {
        _recentFiles.Clear();
        var recent = _settingsService.Get<List<string>>("RecentFiles", new List<string>());
        if (recent != null)
        {
            foreach (var file in recent)
            {
                if (System.IO.File.Exists(file))
                    _recentFiles.Add(file);
            }
        }
        UpdateRecentFilesMenu();
    }

    private void AddRecentFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        _recentFiles.Remove(filePath);
        _recentFiles.Insert(0, filePath);
        if (_recentFiles.Count > 10)
            _recentFiles.RemoveRange(10, _recentFiles.Count - 10);
        _settingsService.Set("RecentFiles", _recentFiles);
        UpdateRecentFilesMenu();
    }

    private void UpdateRecentFilesMenu()
    {
        if (RecentFilesMenu == null) return;

        RecentFilesMenu.Items.Clear();

        if (_recentFiles.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem { Header = Loc.Get("Menu_NoRecentFiles"), IsEnabled = false });
            return;
        }

        foreach (var file in _recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = System.IO.Path.GetFileName(file),
                Tag = file,
                ToolTip = file
            };
            menuItem.Click += (s, e) =>
            {
                if (menuItem.Tag is string path && System.IO.File.Exists(path))
                {
                    if (_isDirty)
                    {
                        var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("Title_OpenRecentFile"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result != MessageBoxResult.Yes) return;
                    }
                    try
                    {
                        var encoding = DetectFileEncoding(path);
                        var content = System.IO.File.ReadAllText(path, encoding);
                        _currentFilePath = path;
                        _gitHubDocument = null;
                        _gistDocument = null;
                        SetEditorText(content);
                        AddRecentFile(path);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Loc.Format("Msg_ErrorOpeningFile", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            RecentFilesMenu.Items.Add(menuItem);
        }
    }

    private string GetDraftPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = System.IO.Path.Combine(appData, "MDEditor");
        System.IO.Directory.CreateDirectory(folder);
        return System.IO.Path.Combine(folder, "draft.md");
    }

    private void AutoSaveDraft()
    {
        // Once the work is saved (or discarded) the draft is stale; leaving it would trigger a
        // false "Crash Recovery" prompt on the next launch.
        if (!_isDirty)
        {
            DeleteDraft();
            return;
        }

        try
        {
            var text = GetEditorText();
            System.IO.File.WriteAllText(GetDraftPath(), text);
        }
        catch { }
    }

    private void DeleteDraft()
    {
        try { System.IO.File.Delete(GetDraftPath()); } catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || !_isDirty)
            return;

        var result = MessageBox.Show(this, Loc.Format("Msg_SaveBeforeClosing", CurrentDocumentName), "MDEditor",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes && _gistDocument != null)
        {
            // Gist saves are asynchronous: stay open, and close once the save has gone through.
            e.Cancel = true;
            SaveGistThenClose();
            return;
        }

        if (result == MessageBoxResult.Yes)
            SaveFile(); // a GitHub document opens the commit dialog

        // Stay open on Cancel, or if the save/commit was cancelled or failed.
        if (result == MessageBoxResult.Cancel || (result == MessageBoxResult.Yes && _isDirty))
            e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        // A normal exit means nothing needs recovering (the user saved or chose not to).
        _autoSaveTimer?.Stop();
        DeleteDraft();
        base.OnClosed(e);
    }

    private void CheckCrashRecovery()
    {
        var draftPath = GetDraftPath();
        if (System.IO.File.Exists(draftPath))
        {
            var result = MessageBox.Show(
                Loc.Get("Msg_RecoverDraft"),
                Loc.Get("Title_CrashRecovery"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var content = System.IO.File.ReadAllText(draftPath);
                    SetEditorText(content);
                    // Recovered text exists nowhere else - keep it unsaved so it gets re-drafted
                    // and closing prompts to save it.
                    MarkDirty();
                }
                catch { }
            }
            // Delete draft after recovery attempt
            try { System.IO.File.Delete(draftPath); } catch { }
        }
    }

    private void MarkdownPreview_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Block file drops and external navigations in the preview panel
        if (e.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private async void StatusZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The 100% ComboBoxItem has IsSelected="True" in XAML, which fires this handler once
        // synchronously during InitializeComponent() - before _settingsService is assigned in
        // the constructor. The explicit StatusZoomCombo.SelectedIndex assignment later in the
        // constructor re-fires this and applies/persists the real (possibly loaded) zoom level.
        if (_settingsService == null)
            return;

        await ApplyZoomAsync(GetCurrentZoomFactor());
        _settingsService.Set("editor.zoomIndex", StatusZoomCombo.SelectedIndex);
    }

    /// <summary>Reads the zoom factor (e.g. 1.25) from the currently selected StatusZoomCombo item.</summary>
    private double GetCurrentZoomFactor()
    {
        if (StatusZoomCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
            double.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zoom))
            return zoom;
        return 1.0;
    }

    /// <summary>Scales the editor font/gutter and the preview's CSS zoom together.</summary>
    private async Task ApplyZoomAsync(double zoom)
    {
        MarkdownEditor.FontSize = _baseEditorFontSize * zoom;
        LineNumberGutter.NumberFontSize = _baseEditorFontSize * zoom;
        LineNumberGutter.Refresh();

        if (MarkdownPreview.CoreWebView2 != null)
        {
            await MarkdownPreview.CoreWebView2.ExecuteScriptAsync(
                $"document.body.style.zoom = '{zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}';");
        }
    }

    private void ZoomIn()
    {
        if (StatusZoomCombo.SelectedIndex < StatusZoomCombo.Items.Count - 1)
            StatusZoomCombo.SelectedIndex++;
    }

    private void ZoomOut()
    {
        if (StatusZoomCombo.SelectedIndex > 0)
            StatusZoomCombo.SelectedIndex--;
    }

    private void ResetZoom()
    {
        StatusZoomCombo.SelectedIndex = DefaultZoomIndex;
    }
}