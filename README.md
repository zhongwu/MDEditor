# MDEditor

A modern **Markdown desktop editor** built with **WPF** and **.NET 10**, featuring a dual-pane interface with live preview, rich formatting toolbar, file management, and GitHub synchronization.

## Current Status (v0.2.0-dev)

| Phase | Status | Notes |
|-------|--------|-------|
| **1. Foundation & Project Setup** | ✅ Complete | WPF app, DI, MVVM, settings, logging |
| **2. Editor Core** | ✅ Complete | Syntax highlight, line numbers, find/replace, go-to-line, word wrap, live preview with KaTeX/Mermaid |
| **3. Toolbar & Formatting** | ✅ Complete | All format commands, menus, toolbar |
| **4. File Operations** | 🔄 In Progress | New/Open/Save/Export not yet implemented |
| **5. GitHub Integration** | ⏳ Not Started | Auth, repo browser, sync |
| **6. Advanced Features** | ⏳ Not Started | Autocomplete, themes, settings |
| **7. Polish & Release** | ⏳ Not Started | Tests, packaging, docs |

## Implemented Features

### Dual-Pane Editing
- **Left pane**: Markdown editor with syntax highlighting (regex-based, 300ms debounced), line numbers, find/replace, go-to-line
- **Right pane**: Live preview with GitHub-flavored Markdown rendering (via WebView2)
- **KaTeX math**: Inline (`$...$`) and block (`$$...$$`) math rendering
- **Mermaid diagrams**: Flowcharts, sequence, pie, gantt, class diagrams
- **Click-to-sync preview**: Click in editor → preview scrolls to corresponding position
- **Real-time updates** (300ms debounced)

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
- **Find/Replace** (Ctrl+F / Ctrl+H): Single-instance dialogs, proper text highlighting, status inline with buttons
- **Go to Line** (Ctrl+G): Paragraph-aware navigation with offset fallback
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