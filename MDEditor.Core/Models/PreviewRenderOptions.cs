namespace MDEditor.Core.Models;

/// <summary>
/// Presentation-only rendering options for the live preview. Unlike
/// <see cref="MarkdownParserOptions"/>, these never affect exported/copied HTML - only the
/// in-app preview pane (and, for custom CSS, the presentation-mode window).
/// </summary>
public readonly record struct PreviewRenderOptions(bool IsDarkTheme, string? CustomCss);
