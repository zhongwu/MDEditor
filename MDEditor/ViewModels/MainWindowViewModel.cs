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
    public Action? ToggleWordWrapCallback { get; set; }

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
        // TODO: Implement new file logic
    }

    [RelayCommand]
    private void OpenFile()
    {
        _loggingService.LogInformation("Open file requested");
        // TODO: Implement open file logic
    }

    [RelayCommand]
    private void SaveFile()
    {
        _loggingService.LogInformation("Save file requested");
        // TODO: Implement save file logic
    }

    [RelayCommand]
    private void SaveAsFile()
    {
        _loggingService.LogInformation("Save As requested");
        // TODO: Implement save file logic
    }

    [RelayCommand]
    private void ExportHtml()
    {
        _loggingService.LogInformation("Export HTML requested");
        // TODO: Implement export HTML
    }

    [RelayCommand]
    private void ExportPdf()
    {
        _loggingService.LogInformation("Export PDF requested");
        // TODO: Implement export PDF
    }

    [RelayCommand]
    private void Exit()
    {
        _loggingService.LogInformation("Exit requested");
        App.Current.Shutdown();
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
        // TODO: Implement preview toggle
    }

    [RelayCommand]
    private void ToggleLineNumbers()
    {
        _loggingService.LogInformation("Toggle line numbers requested");
        // TODO: Implement line numbers toggle
    }

    [RelayCommand]
    private void ZoomIn()
    {
        _loggingService.LogInformation("Zoom in requested");
        // TODO: Implement zoom in
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _loggingService.LogInformation("Zoom out requested");
        // TODO: Implement zoom out
    }

    [RelayCommand]
    private void ResetZoom()
    {
        _loggingService.LogInformation("Reset zoom requested");
        // TODO: Implement reset zoom
    }

    [RelayCommand]
    private void CycleTheme()
    {
        _loggingService.LogInformation("Cycle theme requested");
        // TODO: Implement theme cycling
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

    // Tools commands
    [RelayCommand]
    private void GitHubSync()
    {
        _loggingService.LogInformation("GitHub sync requested");
        // TODO: Implement GitHub sync
    }

    [RelayCommand]
    private void Settings()
    {
        _loggingService.LogInformation("Settings requested");
        // TODO: Implement settings dialog
    }

    // Help commands
    [RelayCommand]
    private void KeyboardShortcuts()
    {
        _loggingService.LogInformation("Keyboard shortcuts requested");
        // TODO: Show keyboard shortcuts dialog
    }

    [RelayCommand]
    private void MarkdownGuide()
    {
        _loggingService.LogInformation("Markdown guide requested");
        // TODO: Show markdown guide
    }

    [RelayCommand]
    private void About()
    {
        _loggingService.LogInformation("About requested");
        // TODO: Show about dialog
    }

    [RelayCommand]
    private void RefreshPreview()
    {
        _loggingService.LogInformation("Refresh preview requested");
        // TODO: Implement preview refresh
    }
}