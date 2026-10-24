namespace MDEditor.Core.Models;

/// <summary>
/// Presentation-only rendering options for the live preview. Unlike
/// <see cref="MarkdownParserOptions"/>, these never affect exported/copied HTML - only the
/// in-app preview pane (and, for custom CSS, the presentation-mode window).
/// </summary>
/// <param name="ImageBaseUrl">
/// When set, relative image URLs resolve against this folder URL (e.g. a GitHub file's folder on
/// raw.githubusercontent.com). Must end with '/'.
/// </param>
/// <param name="ImageRootUrl">Base for root-relative image URLs ("/img.png"); defaults to <paramref name="ImageBaseUrl"/>.</param>
public readonly record struct PreviewRenderOptions(bool IsDarkTheme, string? CustomCss, string? ImageBaseUrl = null, string? ImageRootUrl = null);
