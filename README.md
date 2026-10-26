# MDEditor

A modern **Markdown desktop editor** built with **WPF** and **.NET 10**, featuring a dual-pane interface with live preview, rich formatting toolbar, file management, and GitHub synchronization.

## Download

**Latest release: [v0.4.0](https://github.com/zhongwu/MDEditor/releases/tag/v0.4.0)** — see all versions on the [Releases page](https://github.com/zhongwu/MDEditor/releases).

1. Download `MDEditor-<version>-win-x64-portable.zip` (or `win-arm64` for ARM PCs) from the release **Assets**.
2. Unzip anywhere and run `MDEditor.exe`. No installer or .NET install is needed (self-contained). Windows SmartScreen may warn about an unknown publisher: choose **More info → Run anyway**.
3. Requires Windows 10 1809+ or Windows 11. The Microsoft Edge WebView2 runtime (preinstalled on current Windows) is used for the preview.

The `.msix` packages on the release page are only installable once signed with a trusted certificate; use the portable zip until then. Help → **Check for Updates** tells you when a newer release is out. See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Current Status (v0.4.0)

| Phase | Status | Notes |
|-------|--------|-------|
| **1. Foundation & Project Setup** | ✅ Complete | WPF app, DI, MVVM, settings, logging |
| **2. Editor Core** | ✅ Complete | RichTextBox editor, line numbers, find/replace, go-to-line, word wrap, live preview with KaTeX/Mermaid (syntax highlighting disabled) |
| **3. Toolbar & Formatting** | ✅ Complete | All format commands, menus, toolbar, context menu |
| **4. File Operations** | ✅ Complete | New/Open/Save/Save As/Export HTML/PDF, drag & drop, recent files, auto-save |
| **5. GitHub Integration** | ✅ Complete | OAuth/PAT multi-account auth, repo browser, open/save/sync, conflict handling, file history & diff, gist support |
| **6. Advanced Features** | ✅ Complete | Editor enhancements (autocomplete, bracket matching, smart quotes, minimap), view toggles/zoom, preview (scroll sync, outline, find, print, presentation), settings/themes, localization (en/zh-Hans/zh-Hant) |
| **7. Polish & Release** | 🔄 In Progress | v0.4.0 released: unit tests ~87% coverage, MSIX + portable packaging, GitHub Releases, update check. Open: UI automation, memory-leak & accessibility passes, trusted code signing |

## Implemented Features

### Dual-Pane Editing
- **Left pane**: Markdown editor with line numbers, find/replace, go-to-line (syntax highlighting disabled - needs rework)
- **Right pane**: Live preview with GitHub-flavored Markdown rendering (via WebView2)
- **KaTeX math**: Inline (`$...$`) and block (`$$...$$`) math rendering
- **Mermaid diagrams**: Flowcharts, sequence, pie, gantt, class diagrams
- **Click-to-sync preview**: Click in editor → preview scrolls to corresponding position
- **Real-time updates** (300ms debounced)

### Files, GitHub & Gists
- **File menu** keeps open and save short: *Open...* (local, Ctrl+O), *Open from...* (choose GitHub repository or GitHub Gist), *Recent Files*, *Save*, and *Save As...* (choose This computer or GitHub Gist)
- **Commit...** (Ctrl+Shift+Enter) and **History...** sit in the File menu and appear when a GitHub-hosted document is open; History shows commit history for a repository file or revisions for a gist, with diff and restore
- **GitLab** (gitlab.com or a self-hosted server on any port): connect with a personal access token under Tools > GitLab Accounts..., then File > Open from... > GitLab project. Save / Commit..., conflict handling, merge requests and History... work like the GitHub ones; administrators see every project on the server
- Sign-in, multi-account support and **GitHub Accounts...** are under Tools

### Preview Enhancements
- **Outline / TOC panel** (Ctrl+Shift+E): Collapsible heading tree; click a heading to jump the editor and preview to it; highlights the section containing the caret; resizable
- **Find in preview** (Ctrl+Shift+F, or Ctrl+F while the preview has focus): Highlights all matches, match count, next/previous (Enter / Shift+Enter, F3 / Shift+F3), match case
- **Print preview** (Ctrl+P): Separate window with the browser print preview plus the Windows print dialog
- **Presentation mode** (F5): Full-screen preview with larger type that follows editor changes live; Esc or F5 to exit

### Rich Formatting Toolbar
| Category | Actions |
|----------|---------|
| **Headings** | H1–H6 (Ctrl+1–6) |
| **Text Style** | Bold (Ctrl+B), Italic (Ctrl+I), Strikethrough (Ctrl+Shift+X), Inline Code (Ctrl+`) |
| **Blocks** | Code Block (Ctrl+Shift+C), Blockquote (Ctrl+Q), Horizontal Rule |
| **Lists** | Unordered (Ctrl+Shift+L), Ordered (Ctrl+Shift+O), Task Lists (Ctrl+Shift+T) |
| **Links & Media** | Link (Ctrl+K), Image (Ctrl+Shift+I), Table (Ctrl+Shift+T) |
| **Math** | Inline & Block math (Ctrl+Shift+M) |

### Editor Features
- **Syntax highlighting**: Headings, code blocks, inline code, bold, italic, strikethrough, links, images, blockquotes, lists, horizontal rules
- **Line numbers**: Paragraph-based, aligned with editor content
- **Find/Replace** (Ctrl+F / Ctrl+H): Find bar above the editor — live search while typing, all matches highlighted, match count, next/previous (Enter / Shift+Enter, F3 / Shift+F3), match case, Replace (Enter) / Replace All (Ctrl+Enter, single undo step)
- **Go to Line** (Ctrl+G): Inline bar above the editor; accepts `line` or `line:column`, previews the target while typing, Enter jumps, Esc restores the view
- **Word wrap toggle** (Ctrl+Shift+W)
- **Undo/Redo/Cut/Copy/Paste/Select All**: Standard editing commands

## Tech Stack

| Layer | Technology |
|-------|------------|
| **UI Framework** | WPF (.NET 10) |
| **Language** | C# (.NET 10) |
| **Architecture** | MVVM (CommunityToolkit.Mvvm) |
| **Markdown** | Markdig (GFM + extensions, math, diagrams, footnotes) |
| **Preview** | WebView2 (HTML/CSS, KaTeX, Mermaid) |
| **GitHub API** | Octokit.NET |
| **DI/Logging** | Microsoft.Extensions.* |
| **Settings** | JSON in `%APPDATA%\MDEditor\settings.json` |

## Getting Started

### Prerequisites
- **Windows 10 1809+** (Build 17763) or **Windows 11**
- **Visual Studio 2022 17.8+** with "Desktop development with C#" workload
- **.NET 10 SDK**

### Build & Run

```bash
# Clone the repository
git clone https://github.com/zhongwu/MDEditor.git
cd MDEditor

# Restore and build
dotnet restore
dotnet build

# Run the application
dotnet run --project MDEditor/MDEditor.csproj
```

### Open in Visual Studio
```bash
# Open solution
start MDEditor.slnx
```

## Project Structure

```
MDEditor.slnx
├── MDEditor/                 # WPF Application
│   ├── MainWindow.xaml/.cs   # Main window with dual-pane
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Services/             # Platform services
│   └── FindReplaceDialog.cs  # Find/Replace dialog
├── MDEditor.Core/            # Core Business Logic (Class Library)
│   ├── Services/             # Core Services (Markdown, Settings)
│   └── Interfaces/           # Service Contracts
├── MDEditor.Tests/           # Unit Tests (xUnit + Moq)
└── MDEditor.IntegrationTests/ # UI Automation Tests
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New File |
| `Ctrl+O` | Open File |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+E` | Export |
| `Ctrl+Z / Y` | Undo / Redo |
| `Ctrl+X / C / V` | Cut / Copy / Paste |
| `Ctrl+F / H` | Find / Replace |
| `Ctrl+G` | Go to Line |
| `Ctrl+A` | Select All |
| `Ctrl+1–6` | Heading 1–6 |
| `Ctrl+B / I / ` ` | Bold / Italic / Inline Code |
| `Ctrl+Shift+C` | Code Block |
| `Ctrl+Q` | Blockquote |
| `Ctrl+K` | Insert Link |
| `Ctrl+Shift+L/O/T` | Lists (Unordered/Ordered/Task) |
| `Ctrl+Shift+M` | Math Block |
| `Ctrl+Shift+P` | Toggle Preview |
| `Ctrl+Shift+N` | Toggle Line Numbers |
| `Ctrl+Shift+W` | Toggle Word Wrap |
| `Ctrl+Shift+E` | Toggle Outline Panel |
| `Ctrl+Shift+F` | Find in Preview |
| `Ctrl+P` | Print Preview |
| `F5` | Presentation Mode (Esc to exit) |

## Configuration

Settings are stored in `%APPDATA%\MDEditor\settings.json`:

```json
{
  "theme": "System",
  "editorFontFamily": "Cascadia Code",
  "editorFontSize": 14,
  "previewCss": "github",
  "autoSaveInterval": 30,
  "wordWrap": true,
  "showLineNumbers": true,
  "markdownFlavor": "GitHubFlavored"
}
```

## Development

See [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) for the full roadmap, architecture details, and phase breakdown.

### Running Tests
```bash
dotnet test
```

### Packaging & Releases
```powershell
pwsh tools/Build-Msix.ps1   # self-contained publish -> artifacts/ (portable zip + .msix)
```
- The app version lives in `Directory.Build.props`; add a matching `## [x.y.z]` entry to `CHANGELOG.md`.
- Pushing a tag `vX.Y.Z` runs `.github/workflows/release.yml`: it tests, builds x64 and ARM64 packages, and creates a GitHub release with the changelog section as notes.
- The MSIX is signed only when the repository secrets `WINDOWS_CERT_PFX_BASE64`, `WINDOWS_CERT_PASSWORD` and `WINDOWS_CERT_SUBJECT` are set. For local sideload testing, create a throwaway certificate with `tools/New-DevCertificate.ps1` and pass it to `Build-Msix.ps1 -CertPath`. The portable zip needs no signing.
- Help → Check for Updates compares the running version with the latest GitHub release.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

Licensed under the **Apache License 2.0** - see [LICENSE](LICENSE) for details.

## Acknowledgments

- [Markdig](https://github.com/xoofx/markdig) - Fast, powerful Markdown processor
- [WPF](https://learn.microsoft.com/dotnet/desktop/wpf/) - Windows Presentation Foundation
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) - Evergreen web rendering
- [Octokit.NET](https://github.com/octokit/octokit.net) - GitHub API client
- [CommunityToolkit](https://github.com/CommunityToolkit/) - MVVM, WPF helpers
- [github-markdown-css](https://github.com/sindresorhus/github-markdown-css) - GitHub-style preview CSS

---

**Made with ❤️ for Windows developers who love Markdown**
