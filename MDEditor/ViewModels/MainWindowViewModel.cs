using System;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MDEditor.Core.Interfaces;

namespace MDEditor.ViewModels;

/// <summary>
/// ViewModel for the MainWindow providing commands for all toolbar/menu actions.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigationService;
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Controls the visibility of the Markdown Preview Pane.
    /// </summary>
    [ObservableProperty]
    private bool _isPreviewVisible = true;

    /// <summary>
    /// Controls the visibility of the Markdown editor pane.
    /// </summary>
    [ObservableProperty]
    private bool _isEditorVisible = true;

    /// <summary>
    /// True while the open document came from GitHub; shows the commit/history menu items.
    /// </summary>
    [ObservableProperty]
    private bool _isGitHubDocument;

    /// <summary>
    /// True while the open document is a gist file; shows the gist-only menu items.
    /// </summary>
    [ObservableProperty]
    private bool _isGistDocument;

    /// <summary>
    /// True while the open document lives on GitHub (repository file or gist); shows the shared
    /// History item, which opens whichever history window fits the document.
    /// </summary>
    [ObservableProperty]
    private bool _isRemoteDocument;

    /// <summary>
    /// Automatically saves the preview state to settings whenever it changes.
    /// Also ensures at least one pane (editor or preview) stays visible.
    /// </summary>
    partial void OnIsPreviewVisibleChanged(bool value)
    {
        _settingsService?.Set("editor.preview", value);
        if (!value && !IsEditorVisible)
        {
            IsEditorVisible = true;
        }
    }

    /// <summary>
    /// Automatically saves the editor pane visibility to settings whenever it changes.
    /// Also ensures at least one pane (editor or preview) stays visible.
    /// </summary>
    partial void OnIsEditorVisibleChanged(bool value)
    {
        _settingsService?.Set("editor.editorVisible", value);
        if (!value && !IsPreviewVisible)
        {
            IsPreviewVisible = true;
        }
    }

    /// <summary>
    /// Callback to insert markdown around selected text in the editor.
    /// </summary>
    public Action<string, string>? InsertMarkdownCallback { get; set; }

    /// <summary>
    /// Callback for undo operation.
    /// </summary>
    public Action? UndoCallback { get; set; }

    /// <summary>
    /// Callback for redo operation.
    /// </summary>
    public Action? RedoCallback { get; set; }

    /// <summary>
    /// Callback for cut operation.
    /// </summary>
    public Action? CutCallback { get; set; }

    /// <summary>
    /// Callback for copy operation.
    /// </summary>
    public Action? CopyCallback { get; set; }

    /// <summary>
    /// Callback for paste operation.
    /// </summary>
    public Action? PasteCallback { get; set; }

    /// <summary>
    /// Callback for select all operation.
    /// </summary>
    public Action? SelectAllCallback { get; set; }

    /// <summary>
    /// Callback for find operation.
    /// </summary>
    public Action? FindCallback { get; set; }

    /// <summary>
    /// Callback for replace operation.
    /// </summary>
    public Action? ReplaceCallback { get; set; }

    /// <summary>
    /// Callback for go to line operation.
    /// </summary>
    public Action? GoToLineCallback { get; set; }

    /// <summary>
    /// Callback for word wrap toggle.
    /// </summary>
    public Action? TogglePreviewCallback { get; set; }
    public Action? ToggleWordWrapCallback { get; set; }

    /// <summary>
    /// Callback for line numbers toggle.
    /// </summary>
    public Action? ToggleLineNumbersCallback { get; set; }

    /// <summary>
    /// Callbacks for zoom in/out/reset.
    /// </summary>
    public Action? ZoomInCallback { get; set; }
    public Action? ZoomOutCallback { get; set; }
    public Action? ResetZoomCallback { get; set; }

    /// <summary>
    /// Callback for opening the Settings window.
    /// </summary>
    public Action? SettingsCallback { get; set; }

    /// <summary>
    /// Callbacks for the Help menu.
    /// </summary>
    public Action? MarkdownGuideCallback { get; set; }
    public Action? AboutCallback { get; set; }
    public Action? CheckForUpdatesCallback { get; set; }

    /// <summary>
    /// Callback for opening the GitHub Accounts window.
    /// </summary>
    public Action? GitHubAccountsCallback { get; set; }

    /// <summary>
    /// Callback for the "Open from..." chooser (GitHub repository, gist, other hosts later).
    /// </summary>
    public Action? OpenFromCallback { get; set; }

    /// <summary>
    /// Callback for committing the current GitHub file.
    /// </summary>
    public Action? GitHubSyncCallback { get; set; }

    /// <summary>
    /// Callback for showing the open document's version history (GitHub file or gist revisions).
    /// </summary>
    public Action? HistoryCallback { get; set; }

    /// <summary>
    /// Callback for viewing the open gist on GitHub.
    /// </summary>
    public Action? ViewGistCallback { get; set; }

    /// <summary>
    /// Callback for new file.
    /// </summary>
    public Action? NewFileCallback { get; set; }

    /// <summary>
    /// Callback for open file.
    /// </summary>
    public Action? OpenFileCallback { get; set; }

    /// <summary>
    /// Callback for save file.
    /// </summary>
    public Action? SaveFileCallback { get; set; }

    /// <summary>
    /// Callback for save as file.
    /// </summary>
    public Action? SaveAsFileCallback { get; set; }

    /// <summary>
    /// Callback for export HTML.
    /// </summary>
    public Action? ExportHtmlCallback { get; set; }

    public Action? ExportPdfCallback { get; set; }

    /// <summary>
    /// Callback for toggling the outline (table of contents) panel.
    /// </summary>
    public Action? ToggleOutlineCallback { get; set; }

    /// <summary>
    /// Callback for opening the find bar in the preview pane.
    /// </summary>
    public Action? FindInPreviewCallback { get; set; }

    /// <summary>
    /// Callback for opening the print preview window.
    /// </summary>
    public Action? PrintPreviewCallback { get; set; }

    /// <summary>
    /// Callback for entering/leaving full-screen presentation mode.
    /// </summary>
    public Action? PresentationModeCallback { get; set; }

    public MainWindowViewModel(ISettingsService settingsService, INavigationService navigationService, ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _navigationService = navigationService;
        _loggingService = loggingService;

        _loggingService.LogInformation("MainWindowViewModel initialized");
    }

    // File commands
    [RelayCommand]
    private void NewFile()
    {
        _loggingService.LogInformation("New file requested");
        NewFileCallback?.Invoke();
    }

    [RelayCommand]
    private void OpenFile()
    {
        _loggingService.LogInformation("Open file requested");
        OpenFileCallback?.Invoke();
    }

    [RelayCommand]
    private void SaveFile()
    {
        _loggingService.LogInformation("Save file requested");
        SaveFileCallback?.Invoke();
    }

    [RelayCommand]
    private void SaveAsFile()
    {
        _loggingService.LogInformation("Save As requested");
        SaveAsFileCallback?.Invoke();
    }

    [RelayCommand]
    private void ExportHtml()
    {
        _loggingService.LogInformation("Export HTML requested");
        ExportHtmlCallback?.Invoke();
    }

    [RelayCommand]
    private void ExportPdf()
    {
        _loggingService.LogInformation("Export PDF requested");
        ExportPdfCallback?.Invoke();
    }

    [RelayCommand]
    private void Exit()
    {
        _loggingService.LogInformation("Exit requested");
        // Close (not Shutdown) so MainWindow's unsaved-changes prompt can cancel the exit.
        App.Current.MainWindow?.Close();
    }

    // Edit commands
    [RelayCommand]
    private void Undo()
    {
        _loggingService.LogInformation("Undo requested");
        UndoCallback?.Invoke();
    }

    [RelayCommand]
    private void Redo()
    {
        _loggingService.LogInformation("Redo requested");
        RedoCallback?.Invoke();
    }

    [RelayCommand]
    private void Cut()
    {
        _loggingService.LogInformation("Cut requested");
        CutCallback?.Invoke();
    }

    [RelayCommand]
    private void Copy()
    {
        _loggingService.LogInformation("Copy requested");
        CopyCallback?.Invoke();
    }

    [RelayCommand]
    private void Paste()
    {
        _loggingService.LogInformation("Paste requested");
        PasteCallback?.Invoke();
    }

    [RelayCommand]
    private void Find()
    {
        _loggingService.LogInformation("Find requested");
        FindCallback?.Invoke();
    }

    [RelayCommand]
    private void Replace()
    {
        _loggingService.LogInformation("Replace requested");
        ReplaceCallback?.Invoke();
    }

    [RelayCommand]
    private void GoToLine()
    {
        _loggingService.LogInformation("Go to line requested");
        GoToLineCallback?.Invoke();
    }

    [RelayCommand]
    private void SelectAll()
    {
        _loggingService.LogInformation("Select All requested");
        SelectAllCallback?.Invoke();
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        _loggingService.LogInformation("Toggle word wrap requested");
        ToggleWordWrapCallback?.Invoke();
    }

    // View commands
    [RelayCommand]
    private void TogglePreview()
    {
        _loggingService.LogInformation("Toggle preview requested");

        // Toggle the observable property to trigger the XAML DataTriggers
        IsPreviewVisible = !IsPreviewVisible;

        TogglePreviewCallback?.Invoke();
    }

    [RelayCommand]
    private void ToggleEditor()
    {
        _loggingService.LogInformation("Toggle editor requested");

        // Toggle the observable property to trigger the XAML DataTriggers
        IsEditorVisible = !IsEditorVisible;
    }

    [RelayCommand]
    private void ToggleLineNumbers()
    {
        _loggingService.LogInformation("Toggle line numbers requested");
        ToggleLineNumbersCallback?.Invoke();
    }

    [RelayCommand]
    private void ToggleOutline()
    {
        _loggingService.LogInformation("Toggle outline requested");
        ToggleOutlineCallback?.Invoke();
    }

    [RelayCommand]
    private void FindInPreview()
    {
        _loggingService.LogInformation("Find in preview requested");
        FindInPreviewCallback?.Invoke();
    }

    [RelayCommand]
    private void PrintPreview()
    {
        _loggingService.LogInformation("Print preview requested");
        PrintPreviewCallback?.Invoke();
    }

    [RelayCommand]
    private void PresentationMode()
    {
        _loggingService.LogInformation("Presentation mode requested");
        PresentationModeCallback?.Invoke();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        _loggingService.LogInformation("Zoom in requested");
        ZoomInCallback?.Invoke();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _loggingService.LogInformation("Zoom out requested");
        ZoomOutCallback?.Invoke();
    }

    [RelayCommand]
    private void ResetZoom()
    {
        _loggingService.LogInformation("Reset zoom requested");
        ResetZoomCallback?.Invoke();
    }

    // Format commands
    [RelayCommand]
    private void Heading1()
    {
        _loggingService.LogInformation("Heading 1 requested");
        InsertMarkdownCallback?.Invoke("# ", "\n");
    }

    [RelayCommand]
    private void Heading2()
    {
        _loggingService.LogInformation("Heading 2 requested");
        InsertMarkdownCallback?.Invoke("## ", "\n");
    }

    [RelayCommand]
    private void Heading3()
    {
        _loggingService.LogInformation("Heading 3 requested");
        InsertMarkdownCallback?.Invoke("### ", "\n");
    }

    [RelayCommand]
    private void Heading4()
    {
        _loggingService.LogInformation("Heading 4 requested");
        InsertMarkdownCallback?.Invoke("#### ", "\n");
    }

    [RelayCommand]
    private void Heading5()
    {
        _loggingService.LogInformation("Heading 5 requested");
        InsertMarkdownCallback?.Invoke("##### ", "\n");
    }

    [RelayCommand]
    private void Heading6()
    {
        _loggingService.LogInformation("Heading 6 requested");
        InsertMarkdownCallback?.Invoke("###### ", "\n");
    }

    [RelayCommand]
    private void Bold()
    {
        _loggingService.LogInformation("Bold requested");
        InsertMarkdownCallback?.Invoke("**", "**");
    }

    [RelayCommand]
    private void Italic()
    {
        _loggingService.LogInformation("Italic requested");
        InsertMarkdownCallback?.Invoke("*", "*");
    }

    [RelayCommand]
    private void Strikethrough()
    {
        _loggingService.LogInformation("Strikethrough requested");
        InsertMarkdownCallback?.Invoke("~~", "~~");
    }

    [RelayCommand]
    private void InlineCode()
    {
        _loggingService.LogInformation("Inline code requested");
        InsertMarkdownCallback?.Invoke("`", "`");
    }

    [RelayCommand]
    private void CodeBlock()
    {
        _loggingService.LogInformation("Code block requested");
        InsertMarkdownCallback?.Invoke("```\n", "\n```");
    }

    [RelayCommand]
    private void Blockquote()
    {
        _loggingService.LogInformation("Blockquote requested");
        InsertMarkdownCallback?.Invoke("> ", "\n");
    }

    [RelayCommand]
    private void UnorderedList()
    {
        _loggingService.LogInformation("Unordered list requested");
        InsertMarkdownCallback?.Invoke("- ", "\n");
    }

    [RelayCommand]
    private void OrderedList()
    {
        _loggingService.LogInformation("Ordered list requested");
        InsertMarkdownCallback?.Invoke("1. ", "\n");
    }

    [RelayCommand]
    private void TaskList()
    {
        _loggingService.LogInformation("Task list requested");
        InsertMarkdownCallback?.Invoke("- [ ] ", "\n");
    }

    [RelayCommand]
    private void InsertLink()
    {
        _loggingService.LogInformation("Insert link requested");
        InsertMarkdownCallback?.Invoke("[", "](url)");
    }

    [RelayCommand]
    private void InsertImage()
    {
        _loggingService.LogInformation("Insert image requested");
        InsertMarkdownCallback?.Invoke("![", "](url)");
    }

    [RelayCommand]
    private void InsertTable()
    {
        _loggingService.LogInformation("Insert table requested");
        InsertMarkdownCallback?.Invoke("| Header 1 | Header 2 |\n|----------|----------|\n| Cell 1 | Cell 2 |\n", "\n");
    }

    [RelayCommand]
    private void InsertHorizontalRule()
    {
        _loggingService.LogInformation("Insert horizontal rule requested");
        InsertMarkdownCallback?.Invoke("\n---\n", "\n");
    }

    [RelayCommand]
    private void MathBlock()
    {
        _loggingService.LogInformation("Math block requested");
        InsertMarkdownCallback?.Invoke("$$\n", "\n$$");
    }

    // Tools commands
    [RelayCommand]
    private void GitHubAccounts()
    {
        _loggingService.LogInformation("GitHub accounts requested");
        GitHubAccountsCallback?.Invoke();
    }

    [RelayCommand]
    private void GitHubSync()
    {
        _loggingService.LogInformation("GitHub sync requested");
        GitHubSyncCallback?.Invoke();
    }

    [RelayCommand]
    private void History()
    {
        _loggingService.LogInformation("Document history requested");
        HistoryCallback?.Invoke();
    }


    [RelayCommand]
    private void ViewGist()
    {
        _loggingService.LogInformation("View gist requested");
        ViewGistCallback?.Invoke();
    }


    [RelayCommand]
    private void OpenFrom()
    {
        _loggingService.LogInformation("Open from... requested");
        OpenFromCallback?.Invoke();
    }

    [RelayCommand]
    private void Settings()
    {
        _loggingService.LogInformation("Settings requested");
        SettingsCallback?.Invoke();
    }

    // Help commands
    [RelayCommand]
    private void MarkdownGuide()
    {
        _loggingService.LogInformation("Markdown guide requested");
        MarkdownGuideCallback?.Invoke();
    }

    [RelayCommand]
    private void About()
    {
        _loggingService.LogInformation("About requested");
        AboutCallback?.Invoke();
    }

    [RelayCommand]
    private void CheckForUpdates()
    {
        _loggingService.LogInformation("Check for updates requested");
        CheckForUpdatesCallback?.Invoke();
    }

    [RelayCommand]
    private void RefreshPreview()
    {
        _loggingService.LogInformation("Refresh preview requested");
        // TODO: Implement preview refresh
    }
}