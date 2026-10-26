# Changelog

All notable changes to MDEditor are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).
The release workflow publishes the section matching the pushed `vX.Y.Z` tag as the GitHub release notes.

## [Unreleased]

### Changed
- File menu reorganized: one "Open from..." item and "Save As..." now ask where (GitHub repository / gist / this computer) instead of separate menu items; Commit... (Ctrl+Shift+Enter) and a single History... entry moved from Tools to File.

## [0.3.0]

### Added
- Dual-pane Markdown editor with live WebView2 preview, KaTeX math, Mermaid diagrams, synchronized scrolling, outline panel, minimap, find/replace and go-to-line.
- Formatting toolbar, auto-complete, bracket matching, smart typography, zoom, and hide-editor / hide-preview toggles.
- Print preview, presentation mode, export to HTML and PDF.
- GitHub integration: sign-in with multiple accounts, repository browser, commit/pull-request saving, per-file version history with diff and restore.
- GitHub Gist support: open, create and update gists, with revision history.
- Settings: Light/Dark/System theme, editor font, preview CSS, Markdown flavor toggles, rebindable shortcuts, auto-save interval, line-ending style.
- English, Simplified Chinese and Traditional Chinese UI, including the bundled Markdown guide.
- Help → Check for Updates, which looks for a newer GitHub release.
- Accessibility names on icon-only toolbar buttons and the main panes (Narrator).
- Windows packaging: portable zip and MSIX built by `tools/Build-Msix.ps1`.

### Fixed
- Editor view no longer jumps when clicking, caused by a preview scroll-sync feedback loop.
- A `null` value in `settings.json` now falls back to the default instead of returning `null`.

[0.3.0]: https://github.com/zhongwu/MDEditor/releases/tag/v0.3.0
