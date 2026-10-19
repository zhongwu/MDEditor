# MKEditor

A modern **Markdown desktop editor** built with **WinUI 3** and **Windows App SDK**, featuring a dual-pane interface with live preview, rich formatting toolbar, file management, and GitHub synchronization.

![MKEditor Screenshot](docs/screenshot.png)

## Features

### Dual-Pane Editing
- **Left pane**: Full-featured Markdown editor with syntax highlighting, line numbers, and code folding
- **Right pane**: Live preview with GitHub-flavored Markdown rendering (via WebView2)
- **Synchronized scrolling** between editor and preview
- **Real-time updates** (300ms debounced)

### Rich Formatting Toolbar
| Category | Actions |
|----------|---------|
| **Headings** | H1–H6 (Ctrl+1–6) |
| **Text Style** | Bold (Ctrl+B), Italic (Ctrl+I), Strikethrough, Inline Code |
| **Blocks** | Code Block, Blockquote, Horizontal Rule |
| **Lists** | Unordered, Ordered, Task Lists |
| **Links & Media** | Link (Ctrl+K), Image, Table |
| **Math** | Inline & Block math (KaTeX) |

### File Operations
- New, Open, Save, Save As (Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Shift+S)
- **Recent files** menu (MRU)
- **Auto-save** draft recovery
- **Export** to HTML, PDF
- Drag & drop files to open, images to embed

### GitHub Integration
- OAuth / Personal Access Token authentication
- Browse repositories and files
- Open files directly from GitHub
- Save/commit changes back to GitHub
- View file commit history and diff
- Create/update Gists

### Customization
- Light / Dark / System theme
- Configurable editor font and size
- Custom preview CSS
- Keyboard shortcuts editor
- Markdown flavor settings (GFM, CommonMark)

## Tech Stack

| Layer | Technology |
|-------|------------|
| **UI Framework** | WinUI 3 (Windows App SDK 1.6+) |
| **Language** | C# (.NET 8/9) |
| **Architecture** | MVVM (CommunityToolkit.Mvvm) |
| **Markdown** | Markdig (GFM + extensions) |
| **Preview** | WebView2 (HTML/CSS rendering) |
| **GitHub API** | Octokit.NET |
| **DI/Logging** | Microsoft.Extensions.* |
| **Packaging** | MSIX |

## Getting Started

### Prerequisites
- **Windows 10 1809+** (Build 17763) or **Windows 11**
- **Visual Studio 2022 17.8+** with "Desktop development with C#" workload
- **.NET 8/9 SDK**
- **Windows App SDK 1.6+** (installed via VS or NuGet)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/<your-username>/MKEditor.git
cd MKEditor

# Restore and build
dotnet restore
dotnet build

# Run the application
dotnet run --project MKEditor/MKEditor.csproj
```

### Open in Visual Studio
```bash
# Open solution
start MKEditor.sln
```

## Project Structure

```
MKEditor.sln
├── MKEditor/                 # WinUI 3 Application
│   ├── Views/                # XAML Views (MainWindow, Dialogs, etc.)
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Services/             # Platform services (File, Settings, Theme)
│   ├── Converters/           # XAML Value Converters
│   ├── Behaviors/            # XAML Behaviors
│   └── Resources/            # Styles, Templates, Themes
├── MKEditor.Core/            # Core Business Logic (Class Library)
│   ├── Models/               # Domain Models (Document, GitHub, Settings)
│   ├── Services/             # Core Services (Markdown, GitHub, File)
│   ├── Parsers/              # Markdig Pipeline Configuration
│   └── Interfaces/           # Service Contracts
├── MKEditor.Tests/           # Unit Tests (xUnit + Moq)
└── MKEditor.IntegrationTests/ # UI Automation Tests
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
| `Ctrl+F / H` | Find / Replace |
| `Ctrl+1–6` | Heading 1–6 |
| `Ctrl+B / I / ` ` | Bold / Italic / Inline Code |
| `Ctrl+K` | Insert Link |
| `Ctrl+Shift+P` | Toggle Preview |

## Configuration

Settings are stored in `%APPDATA%\MKEditor\settings.json`:

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

### Creating MSIX Package
```bash
# Requires code signing certificate for distribution
dotnet publish -c Release -r win10-x64 --self-contained true
# Then use MSIX Packaging Tool or VS "Publish" → "Create App Packages"
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for coding standards and PR guidelines.

## License

Licensed under the **Apache License 2.0** - see [LICENSE](LICENSE) for details.

## Acknowledgments

- [Markdig](https://github.com/xoofx/markdig) - Fast, powerful Markdown processor
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) - Modern Windows app platform
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) - Evergreen web rendering
- [Octokit.NET](https://github.com/octokit/octokit.net) - GitHub API client
- [CommunityToolkit](https://github.com/CommunityToolkit/) - MVVM, WinUI helpers
- [github-markdown-css](https://github.com/sindresorhus/github-markdown-css) - GitHub-style preview CSS

---

**Made with ❤️ for Windows developers who love Markdown**