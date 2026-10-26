# Changelog

All notable changes to MDEditor are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).
The release workflow publishes the section matching the pushed `vX.Y.Z` tag as the GitHub release notes.

## [Unreleased]

### Added
- Auto-update check via `IUpdateService` / `UpdateService` (GitHub Releases), with `UpdateServiceTests`.
- MSIX packaging pipeline (`tools/Build-Msix.ps1`, `MDEditor/Packaging/`), dev certificate helper, and release/CI GitHub Actions workflows.
- Accessibility: `AutomationProperties.Name` on icon-only toolbar buttons and main panes (outline, editor, preview).
- Performance regression guards for rendering, word counting, and line-ending normalization on 10 MB documents.
- `SettingsService` constructor accepting an explicit settings file path (for test isolation).

### Changed
- README "Current Status" table updated to reflect completed phases (v0.9.0-dev).
- Large-document performance test now uses unique headings to avoid Markdig quadratic auto-ids.

### Fixed
- A `null` value in `settings.json` now falls back to the default instead of returning `null`.
- Removed duplicate xUnit `InlineData` rows in `TextStatisticsTests`.

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
- Windows packaging: portable zip and MSIX built by `tools/Build-Msix.ps1`.

### Fixed
- Editor view no longer jumps when clicking, caused by a preview scroll-sync feedback loop.
- A `null` value in `settings.json` now falls back to the default instead of returning `null`.

[0.3.0]: https://github.com/zhongwu/MDEditor/releases/tag/v0.3.0
