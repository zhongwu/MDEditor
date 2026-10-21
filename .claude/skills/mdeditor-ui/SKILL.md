---
name: mdeditor-ui
description: WPF UI patterns and conventions for MDEditor. Use when working on XAML layouts, dialogs, toolbar, menus, or styling.
allowed-tools: Read, Glob, Grep, Edit, Write
---

# MDEditor UI Patterns

## XAML Conventions

- Use `DynamicResource` for system colors: `{DynamicResource {x:Static SystemColors.ControlLightBrushKey}}`
- Font family: `Cascadia Code, Consolas, monospace` for editor
- Font size: 14pt for editor and line numbers
- Line height: 18px for both editor and line numbers (matched)

## Dialog Pattern

All dialogs are single-instance, tracked in MainWindow:

```csharp
private FindReplaceDialog? _findDialog;

private void ShowFindDialog()
{
    if (_findDialog != null && _findDialog.IsVisible)
    {
        _findDialog.Activate();
        _findDialog.Focus();
        return;
    }
    _findDialog = new FindReplaceDialog(MarkdownEditor, false);
    _findDialog.Owner = this;
    _findDialog.Closed += (s, e) => _findDialog = null;
    _findDialog.Show();
}
```

## Editor Layout

```
Grid (MainContentGrid)
├── EditorColumn (Width: *)
│   └── EditorPane (Grid)
│       ├── Row 0: Header bar (EDITOR label, filename, dirty indicator)
│       └── Row 1: Border
│           └── Grid
│               ├── Column 0: LineNumbers (TextBlock, Auto width)
│               └── Column 1: MarkdownEditor (RichTextBox, *)
├── SplitterColumn (Width: Auto, 6px)
└── PreviewColumn (Width: *)
    └── PreviewPane (Grid)
        ├── Row 0: Header bar (PREVIEW label)
        └── Row 1: Border
            └── MarkdownPreview (WebView2)
```

## Toolbar Pattern

Toolbar buttons use Segoe MDL2 Assets font for icons:
```xml
<Button Command="{Binding BoldCommand}" ToolTip="Bold (Ctrl+B)">
    <Button.Content>
        <TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE8DD;" />
    </Button.Content>
</Button>
```

Text-based icons (for better visibility):
```xml
<Button Command="{Binding BlockquoteCommand}" ToolTip="Blockquote (Ctrl+Q)">
    <Button.Content>
        <TextBlock Text="&gt;" VerticalAlignment="Center" />
    </Button.Content>
</Button>
```

## Context Menu

Right-click context menu on RichTextBox with format commands. Defined in XAML:
```xml
<RichTextBox.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Bold" Command="{Binding BoldCommand}" />
        ...
    </ContextMenu>
</RichTextBox.ContextMenu>
```

## Status Bar

StatusBar with StatusBarItem for:
- Cursor position (Ln X, Col Y)
- File encoding (UTF-8)
- Word count
- Line ending (LF/CRLF)
- Zoom combo (50%-200%)

## Common UI Patterns

### Callback wiring (ViewModel → View):
```csharp
// In MainWindow constructor
ViewModel.FindCallback = ShowFindDialog;
ViewModel.SaveFileCallback = SaveFile;
```

### XAML bindings:
```xml
<MenuItem Header="Find" Command="{Binding FindCommand}" />
```

### File dirty indicator:
- Title bar: `MDEditor - filename.md ●` (● when dirty)
- Editor header: red ● indicator (Visibility toggled)