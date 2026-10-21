---
name: mdeditor-fileops
description: File management patterns for MDEditor. Use when implementing file operations, auto-save, recent files, or export features.
allowed-tools: Read, Glob, Grep, Edit, Write
---

# MDEditor File Operations

## File State Tracking

```csharp
private string? _currentFilePath = null;
private bool _isDirty = false;
```

## Title Bar Update

```csharp
private void UpdateTitleBar()
{
    var fileName = string.IsNullOrEmpty(_currentFilePath)
        ? "Untitled.md"
        : System.IO.Path.GetFileName(_currentFilePath);
    var dirty = _isDirty ? " ●" : "";
    Title = $"MDEditor - {fileName}{dirty}";
    EditorFileName.Text = fileName;
    EditorDirtyIndicator.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;
}
```

## Dirty State

```csharp
private void MarkDirty()
{
    if (!_isDirty)
    {
        _isDirty = true;
        UpdateTitleBar();
    }
}

private void SetEditorText(string text)
{
    var range = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
    range.Text = text;
    _isDirty = false;
    UpdateTitleBar();
}
```

## File Operations

### New File
- Check for unsaved changes (prompt user)
- Reset `_currentFilePath = null`
- Clear editor text
- Update title bar

### Open File
- Check for unsaved changes (prompt user)
- Show `OpenFileDialog` with filters: `.md`, `.markdown`, `.txt`, `*.*`
- Read file content with `File.ReadAllText()`
- Set `_currentFilePath` and update editor

### Save File
- If `_currentFilePath` is null → call SaveAsFile()
- Otherwise write with `File.WriteAllText()`
- Set `_isDirty = false`, update title bar

### Save As
- Show `SaveFileDialog` with filters
- Pre-fill filename from current path or "Untitled.md"
- Write file, update `_currentFilePath` and dirty state

### Export HTML
- Show `SaveFileDialog` for .html
- Convert markdown to HTML via `_markdownParser.ToHtmlDocument()`
- Write to file, show success message

## Unsaved Changes Prompt

```csharp
private bool PromptUnsavedChanges()
{
    if (!_isDirty) return true;
    var result = MessageBox.Show(
        "You have unsaved changes. Discard them?",
        "Unsaved Changes",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);
    return result == MessageBoxResult.Yes;
}
```

## Remaining Features (Not Implemented)

- Auto-save draft (periodic, recover on crash)
- Recent Files menu (MRU list, max 10)
- File encoding detection (UTF-8 default, fallback)
- Export to PDF
- Drag & drop files to open
- Drag & drop images to embed