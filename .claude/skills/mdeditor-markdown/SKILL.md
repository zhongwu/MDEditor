---
name: mdeditor-markdown
description: Markdig pipeline configuration and Markdown processing for MDEditor. Use when adding Markdown extensions, modifying the parser, or working with preview rendering.
allowed-tools: Read, Glob, Grep, Edit, Write
---

# MDEditor Markdown Processing

## Pipeline Configuration

Located in: `MDEditor.Core/Services/MarkdownParser.cs`

```csharp
_pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .UseEmojiAndSmiley()
    .UseGridTables()
    .UsePipeTables()
    .UseMathematics()
    .UseDiagrams()
    .UseFootnotes()
    .UseDefinitionLists()
    .UseAutoIdentifiers()
    .UseGenericAttributes()
    .UseAbbreviations()
    .UseCitations()
    .UseCustomContainers()
    .UseFigures()
    .UseMediaLinks()
    .UseSmartyPants()
    .UseTaskLists()
    .Build();
```

## Key Methods

- `ToHtml(markdown)` - Returns HTML body only
- `ToHtmlDocument(markdown)` - Returns complete HTML document with CSS/JS

## CDN Dependencies (in HTML template)

- **KaTeX**: Math rendering (`katex@0.16.9`)
- **Mermaid**: Diagram rendering (`mermaid@10`)
- **GitHub CSS**: GitHub-style markdown styling

## Adding New Extensions

1. Add `.UseXxx()` to the pipeline builder
2. Add corresponding CSS to `ToHtmlDocument` if needed
3. Add JavaScript initialization if needed
4. Update DEVELOPMENT_PLAN.md

## Markdig Extension Naming

Markdig extensions follow the pattern:
- `.UseAdvancedExtensions()` - GFM (tables, task lists, strikethrough, etc.)
- `.UseXxx()` - Specific extensions (Mathematics, Diagrams, etc.)

## Preview Architecture

1. `MarkdownEditor_TextChanged` → debounced 1000ms → `UpdatePreview()`
2. `UpdatePreview()` → runs `ToHtml()` on background thread
3. Updates WebView2 body via `ExecuteScriptAsync` (in-place DOM update)
4. KaTeX and Mermaid re-render after content update
5. Content-change detection skips unnecessary updates

## Common Issues

- **Mermaid rendering**: Requires `mermaid.run()` after DOM update
- **KaTeX rendering**: Requires `renderMathInElement()` after DOM update
- **UTF-8 encoding**: Use base64 encoding for JS string injection
- **Performance**: In-place update is faster than full page reload