# MDEditor Development Plan

## Project Overview
**MDEditor** - A modern Markdown desktop editor built with WPF (.NET 8/9), featuring a dual-pane interface with live preview, toolbar for formatting, file operations, and GitHub version synchronization.

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **UI Framework** | WPF (.NET 8/9) |
| **Language** | C# (.NET 8/9) |
| **Markdown Parser** | Markdig (CommonMark + GitHub Flavored Markdown) |
| **Syntax Highlighting** | Markdig + WPF RichTextBox |
| **File Operations** | System.IO APIs |
| **GitHub Integration** | Octokit.NET |
| **Settings/Config** | Windows.Storage.ApplicationData / JSON |
| **Architecture** | MVVM with CommunityToolkit.Mvvm |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Testing** | xUnit, Moq, Microsoft.VisualStudio.TestPlatform |
| **Preview** | WebView2 (Edge Chromium) |

---

## Development Phases

### Phase 1: Foundation & Project Setup (Week 1-2)

#### 1.1 Project Initialization
- [ ] Create WPF Desktop project targeting .NET 8/9
- [ ] Set up solution structure:
  ```
  MDEditor.slnx
  ├── MDEditor (WPF App)
  ├── MDEditor.Core (Class Library - Business Logic)
  ├── MDEditor.Tests (Unit Tests)
  └── MDEditor.IntegrationTests (UI Tests)
  ```
- [ ] Configure NuGet packages:
  - CommunityToolkit.Mvvm
  - CommunityToolkit.Wpf
  - Markdig
  - Octokit.NET
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging
  - Microsoft.Web.WebView2
  - Microsoft.Xaml.Behaviors.Wpf

#### 1.2 Core Architecture Setup
- [ ] Implement MVVM base classes (ObservableObject, ObservableValidator)
- [ ] Set up Dependency Injection container
- [ ] Configure logging (Serilog or Microsoft.Extensions.Logging)
- [ ] Create application settings service
- [ ] Implement navigation service (if multi-page needed)

#### 1.3 UI Shell
- [ ] Create main window with WPF style
- [ ] Implement responsive two-pane layout (Grid with GridSplitter)
- [ ] Add toolbar with command bindings
- [ ] Implement status bar (line/column, encoding, word count)

---

### Phase 2: Editor Core (Week 2-3)

#### 2.1 Markdown Text Editor (Left Pane)
- [ ] Use `RichTextBox` with monospace font
- [ ] Implement syntax highlighting via Markdig + custom highlighting
- [ ] Line numbers gutter
- [ ] Basic editing features:
  - [ ] Undo/Redo
  - [ ] Find/Replace (Ctrl+F, Ctrl+H)
  - [ ] Go to Line (Ctrl+G)
  - [ ] Word wrap toggle
  - [ ] Tab/Shift+Tab for indentation

#### 2.2 Live Preview (Right Pane)
- [ ] `WebView2` for full HTML rendering
  - Load Markdig-rendered HTML with custom CSS
  - Support GitHub-style CSS (github-markdown-css)
  - Handle internal links, images, math (KaTeX), mermaid
- [ ] Synchronized scrolling (both panes scroll together)
- [ ] Auto-refresh on text change (debounced 300ms)

#### 2.3 Markdown Processing Pipeline
- [ ] Configure Markdig pipeline:
  - CommonMark core
  - GitHub Flavored Markdown extensions
  - Pipe tables, task lists, strikethrough
  - Emoji support
  - Math formulas ($$...$$)
  - Mermaid diagrams
  - Footnotes
  - Definition lists
- [ ] Custom renderers for WPF/WebView2

---

### Phase 3: Toolbar & Formatting (Week 3-4)

#### 3.1 Toolbar Structure
```
[File] [Edit] [View] [Format] [Tools] [Help]
```
- **File**: New, Open, Save, Save As, Export, Recent Files, Exit
- **Edit**: Undo, Redo, Cut, Copy, Paste, Find, Replace, Select All
- **View**: Toggle Preview, Toggle Line Numbers, Word Wrap, Zoom, Theme
- **Format**: Heading 1-6, Bold, Italic, Strikethrough, Code, Quote, List, Link, Image, Table, HR, Math
- **Tools**: GitHub Sync, Settings, Extensions
- **Help**: About, Keyboard Shortcuts, Markdown Guide

#### 3.2 Format Commands (Apply to Selection)
| Command | Markdown | Shortcut |
|---------|----------|----------|
| Heading 1-6 | `# ` .. `###### ` | Ctrl+1..6 |
| Bold | `**text**` | Ctrl+B |
| Italic | `*text*` | Ctrl+I |
| Strikethrough | `~~text~~` | Ctrl+Shift+X |
| Inline Code | `` `code` `` | Ctrl+` |
| Code Block | ```lang\ncode\n``` | Ctrl+Shift+C |
| Blockquote | `> ` | Ctrl+Q |
| Unordered List | `- ` | Ctrl+Shift+L |
| Ordered List | `1. ` | Ctrl+Shift+O |
| Task List | `- [ ] ` | Ctrl+Shift+T |
| Link | `[text](url)` | Ctrl+K |
| Image | `![alt](url)` | Ctrl+Shift+I |
| Table | Pipe table template | Ctrl+Shift+T |
| Horizontal Rule | `---` | Ctrl+Shift+H |
| Math Block | `$$...$$` | Ctrl+Shift+M |

#### 3.3 Context Menu
- Right-click menu with format options
- "Copy as HTML" / "Copy as Markdown"

---

### Phase 4: File Operations (Week 4-5)

#### 4.1 File Management
- [ ] New File (Ctrl+N) - untitled, track dirty state
- [ ] Open File (Ctrl+O) - OpenFileDialog, support .md, .markdown, .txt
- [ ] Save (Ctrl+S) - Save to existing path
- [ ] Save As (Ctrl+Shift+S) - SaveFileDialog
- [ ] Auto-save draft (periodic, recover on crash)
- [ ] Recent Files menu (MRU list, max 10)
- [ ] File encoding detection (UTF-8 default, fallback)

#### 4.2 Export Features
- [ ] Export to HTML (with embedded CSS)
- [ ] Export to PDF (via WebView2 print or PDF library)
- [ ] Export to Word (.docx) - optional
- [ ] Copy as HTML to clipboard

#### 4.3 Drag & Drop
- [ ] Drop .md files to open
- [ ] Drop images to embed (copy to assets folder, insert link)
- [ ] Drag tabs to reorder (if tabbed interface)

---

### Phase 5: GitHub Integration (Week 5-6)

#### 5.1 Authentication
- [ ] GitHub OAuth (device flow or web auth)
- [ ] Personal Access Token (PAT) support
- [ ] Secure token storage (Windows Credential Manager / DPAPI)
- [ ] Multi-account support

#### 5.2 Repository Operations
- [ ] Browse repositories (list user/org repos)
- [ ] Browse files in repository (tree view)
- [ ] Open file from GitHub (download to temp, open in editor)
- [ ] Save/Sync back to GitHub:
  - [ ] Commit changes with message
  - [ ] Create new branch / PR option
  - [ ] Handle merge conflicts (basic)

#### 5.3 Version History
- [ ] View commit history for current file
- [ ] Compare with previous version (diff view)
- [ ] Restore previous version

#### 5.4 Gist Support (Bonus)
- [ ] Create/Update secret/public gists
- [ ] Open gist files

---

### Phase 6: Advanced Features (Week 6-7)

#### 6.1 Editor Enhancements
- [ ] Auto-complete for:
  - Markdown syntax
  - Emoji (:smile:)
  - @mentions (if GitHub context)
  - File paths (for links/images)
- [ ] Multiple cursors (Alt+Click)
- [ ] Minimap (code overview)
- [ ] Bracket matching
- [ ] Smart quotes/dashes

#### 6.2 Preview Enhancements
- [ ] Outline/TOC panel (collapsible)
- [ ] Find in preview
- [ ] Print preview
- [ ] Presentation mode (fullscreen preview)

#### 6.3 Settings & Customization
- [ ] Theme: Light / Dark / System / Custom
- [ ] Editor font family/size
- [ ] Preview CSS customization
- [ ] Keyboard shortcuts editor
- [ ] Markdown flavor settings (GFM, CommonMark, etc.)
- [ ] Auto-save interval
- [ ] Line ending style (LF/CRLF)

---

### Phase 7: Polish & Release (Week 7-8)

#### 7.1 Quality Assurance
- [ ] Unit tests for core services (>80% coverage)
- [ ] UI automation tests for critical flows
- [ ] Performance profiling (large files >10MB)
- [ ] Memory leak detection
- [ ] Accessibility (Narrator, high contrast, keyboard nav)

#### 7.2 Packaging & Deployment
- [ ] MSIX packaging with WPF
- [ ] Code signing certificate
- [ ] Auto-update (MSIX auto-update or GitHub Releases)
- [ ] Microsoft Store submission (optional)
- [ ] GitHub Releases with changelog

#### 7.3 Documentation
- [ ] User guide (Markdown, bundled in app)
- [ ] Keyboard shortcuts reference
- [ ] Contributing guide
- [ ] Architecture decision records

---

## Detailed Technical Specifications

### Data Models

```csharp
// Core document model
public class MarkdownDocument
{
    public string FilePath { get; set; }
    public string Content { get; set; }
    public bool IsDirty { get; set; }
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public DateTime LastModified { get; set; }
    public GitHubFileInfo GitHubInfo { get; set; } // if from GitHub
}

// GitHub integration models
public class GitHubRepository
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Owner { get; set; }
    public bool IsPrivate { get; set; }
    public string DefaultBranch { get; set; }
}

public class GitHubFileInfo
{
    public string RepositoryFullName { get; set; }
    public string Path { get; set; }
    public string Branch { get; set; }
    public string Sha { get; set; }
    public string CommitSha { get; set; }
}
```

### Key Services (Interfaces)

```csharp
// Core services
public interface IMarkdownParser { string ToHtml(string markdown); }
public interface IFileService { 
    Task<MarkdownDocument> OpenAsync(string path);
    Task SaveAsync(MarkdownDocument doc, string path);
    Task<string> ExportHtmlAsync(MarkdownDocument doc);
}
public interface IGitHubService {
    Task AuthenticateAsync();
    Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync();
    Task<MarkdownDocument> OpenFromGitHubAsync(string repo, string path, string branch);
    Task SyncToGitHubAsync(MarkdownDocument doc, string commitMessage);
    Task<IReadOnlyList<GitHubCommit>> GetFileHistoryAsync(GitHubFileInfo info);
}
public interface ISettingsService {
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
}
public interface IRecentFilesService {
    IReadOnlyList<string> GetRecentFiles();
    void AddRecentFile(string path);
}
```

### UI Component Structure (WPF)

```
MainWindow
├── TitleBar (Custom)
├── MenuBar / Menu
├── Toolbar (ToolBar / ToolBarTray)
├── MainContent (Grid)
│   ├── EditorPane (Left)
│   │   ├── LineNumberGutter
│   │   └── MarkdownEditor (RichTextBox)
│   ├── GridSplitter
│   └── PreviewPane (Right)
│       └── MarkdownPreview (WebView2)
├── StatusBar
│   ├── CursorPosition
│   ├── Encoding
│   ├── WordCount
│   ├── ThemeToggle
│   └── ZoomControl
└── Dialogs/Flyouts
    ├── FindReplaceDialog
    ├── GitHubAuthDialog
    ├── GitHubRepoBrowser
    ├── SettingsDialog
    └── AboutDialog
```

---

## Keyboard Shortcuts Reference

| Shortcut | Action |
|----------|--------|
| **File** | |
| Ctrl+N | New File |
| Ctrl+O | Open File |
| Ctrl+S | Save |
| Ctrl+Shift+S | Save As |
| Ctrl+E | Export |
| Ctrl+W | Close Tab/Window |
| **Edit** | |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+X / C / V | Cut / Copy / Paste |
| Ctrl+F | Find |
| Ctrl+H | Replace |
| Ctrl+G | Go to Line |
| Ctrl+A | Select All |
| **View** | |
| Ctrl+Shift+P | Toggle Preview |
| Ctrl+Shift+N | Toggle Line Numbers |
| Ctrl+Shift+W | Toggle Word Wrap |
| Ctrl++ / Ctrl+- | Zoom In/Out |
| Ctrl+0 | Reset Zoom |
| **Format** | |
| Ctrl+1..6 | Heading 1-6 |
| Ctrl+B | Bold |
| Ctrl+I | Italic |
| Ctrl+` | Inline Code |
| Ctrl+Shift+C | Code Block |
| Ctrl+Q | Blockquote |
| Ctrl+K | Insert Link |
| Ctrl+Shift+I | Insert Image |

---

## Risk Assessment & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Large file performance (>10MB) | Medium | Medium | Virtualized editor, lazy loading, background parsing |
| GitHub API rate limits | Medium | Medium | Implement caching, conditional requests, user feedback |
| Markdig rendering differences from GitHub | Low | Medium | Test with GitHub samples; allow custom CSS |
| WPF rendering performance | Low | Medium | Use virtualized controls, optimize layout |

---

## Success Criteria (MVP)

1. ✅ Dual-pane editor with live preview (<300ms latency)
2. ✅ Full toolbar with format commands applying to selection
3. ✅ Open/Save/Save As/Export to HTML/PDF
4. ✅ GitHub authentication and file open/save
5. ✅ Settings persist (theme, font, recent files)
6. ✅ Runs on Windows 10 1809+ / Windows 11
7. ✅ MSIX package installs and updates correctly

---

## Future Enhancements (Post-MVP)

- [ ] Plugin system (custom renderers, formatters)
- [ ] Multi-tab interface
- [ ] Split view (horizontal/vertical)
- [ ] Vim mode
- [ ] Collaboration (real-time co-editing)
- [ ] Diagram editing (mermaid live editor)
- [ ] Notebook interface (code blocks execution)
- [ ] Mobile companion app (WPF mobile)

---

## Estimated Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Foundation | 2 weeks | 2 weeks |
| Phase 2: Editor Core | 2 weeks | 4 weeks |
| Phase 3: Toolbar & Formatting | 2 weeks | 6 weeks |
| Phase 4: File Operations | 2 weeks | 8 weeks |
| Phase 5: GitHub Integration | 2 weeks | 10 weeks |
| Phase 6: Advanced Features | 2 weeks | 12 weeks |
| Phase 7: Polish & Release | 2 weeks | 14 weeks |

**Total: ~14 weeks (3.5 months) for MVP**

---

## Getting Started Commands

```bash
# Create solution structure
dotnet new sln -n MDEditor
dotnet new wpf -n MDEditor -o MDEditor
dotnet new classlib -n MDEditor.Core -o MDEditor.Core
dotnet new xunit -n MDEditor.Tests -o MDEditor.Tests

# Add projects to solution
dotnet sln add MDEditor/MDEditor.csproj
dotnet sln add MDEditor.Core/MDEditor.Core.csproj
dotnet sln add MDEditor.Tests/MDEditor.Tests.csproj

# Add core packages
dotnet add MDEditor.Core package Markdig
dotnet add MDEditor.Core package Octokit
dotnet add MDEditor.Core package CommunityToolkit.Mvvm
dotnet add MDEditor.Core package Microsoft.Extensions.DependencyInjection
dotnet add MDEditor package Microsoft.Web.WebView2
dotnet add MDEditor package CommunityToolkit.Wpf
dotnet add MDEditor package Microsoft.Xaml.Behaviors.Wpf

# Build and run
dotnet build
dotnet run --project MDEditor/MDEditor.csproj
```

---

*Document Version: 2.0*  
*Created: 2026*  
*Last Updated: 2026*