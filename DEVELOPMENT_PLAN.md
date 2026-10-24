# MDEditor Development Plan

## ⚠️ AI Agent Rules

> **IMPORTANT**: Any code changes, commits, or pushes to this repository **require explicit user review and approval** before execution. The AI agent must:
> 1. Make changes locally
> 2. Build and verify
> 3. Report results to the user
> 4. **Wait for explicit "push" or "looks good, push" approval** before committing or pushing
> 5. Never push without explicit user permission

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

### Phase 1: Foundation & Project Setup (Week 1-2) ✅ COMPLETED

#### 1.1 Project Initialization
- [x] Create WPF Desktop project targeting .NET 10
- [x] Set up solution structure:
  ```
  MDEditor.slnx
  ├── MDEditor (WPF App)
  ├── MDEditor.Core (Class Library - Business Logic)
  ├── MDEditor.Tests (Unit Tests)
  └── MDEditor.IntegrationTests (UI Tests)
  ```
- [x] Configure NuGet packages:
  - CommunityToolkit.Mvvm
  - CommunityToolkit.Wpf
  - Markdig
  - Octokit.NET
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging
  - Microsoft.Web.WebView2
  - Microsoft.Xaml.Behaviors.Wpf

#### 1.2 Core Architecture Setup
- [x] Implement MVVM base classes (ObservableObject, ObservableValidator)
- [x] Set up Dependency Injection container
- [x] Configure logging (Microsoft.Extensions.Logging)
- [x] Create application settings service (JSON in %APPDATA%)
- [x] Implement navigation service

#### 1.3 UI Shell
- [x] Create main window with WPF style
- [x] Implement responsive two-pane layout (Grid with GridSplitter)
- [x] Add toolbar with command bindings
- [x] Implement status bar (line/column, encoding, word count, zoom)

---

### Phase 2: Editor Core (Week 2-3) ✅ MOSTLY COMPLETED

#### 2.1 Markdown Text Editor (Left Pane)
- [x] Use `RichTextBox` with monospace font (Cascadia Code)
- [ ] Implement syntax highlighting (disabled - causes scroll issues, needs better approach)
  - ~~Headings, code blocks, inline code, bold, italic, strikethrough~~
  - ~~Links, images, blockquotes, lists (unordered/ordered), horizontal rules~~
  - **TODO**: Implement using AvalonEdit or Markdig AST with incremental updates
- [x] Line numbers gutter (paragraph-based, aligned with editor)
- [x] Basic editing features:
  - [x] Undo/Redo
  - [x] Find/Replace (Ctrl+F, Ctrl+H) - inline find bar above the editor (live search, highlights all matches, match count, F3 navigation, replace / replace all)
  - [x] Go to Line (Ctrl+G) - inline bar (line or line:column, live preview, Esc restores view)
  - [x] Word wrap toggle
  - [x] Tab/Shift+Tab for indentation

#### 2.2 Live Preview (Right Pane)
- [x] `WebView2` for full HTML rendering
  - Load Markdig-rendered HTML with GitHub-style CSS
  - Auto-refresh on text change (debounced 300ms)
- [x] Click-to-sync preview (click in editor → preview scrolls to corresponding position)
- [x] Handle internal links, images, math (KaTeX), mermaid
- [x] Emoji support via Markdig UseEmojiAndSmiley

#### 2.3 Markdown Processing Pipeline ✅ COMPLETED
- [x] Configure Markdig pipeline:
  - CommonMark core
  - GitHub Flavored Markdown extensions (UseAdvancedExtensions)
  - Pipe tables, task lists, strikethrough
  - Emoji support (UseEmojiAndSmiley)
  - Math formulas ($$...$$) (UseMathematics + KaTeX)
  - Mermaid diagrams (UseDiagrams + Mermaid.js)
  - Footnotes (UseFootnotes)
  - Definition lists (UseDefinitionLists)
  - Grid tables (UseGridTables)
  - Auto identifiers (UseAutoIdentifiers)
  - Generic attributes (UseGenericAttributes)
  - Abbreviations (UseAbbreviations)
  - Citations (UseCitations)
  - Custom containers (UseCustomContainers)
  - Figures (UseFigures)
  - Media links (UseMediaLinks)
  - SmartyPants (UseSmartyPants)
- [x] Custom CSS for WPF/WebView2 (GitHub-style + figures, footnotes, citations, alerts)

---

### Phase 3: Toolbar & Formatting (Week 3-4) ✅ COMPLETED

#### 3.1 Toolbar Structure
```
[File] [Edit] [View] [Format] [Tools] [Help]
```
- **File**: New, Open, Save, Save As, Export, Exit
- **Edit**: Undo, Redo, Cut, Copy, Paste, Find, Replace, Select All, Go to Line
- **View**: Toggle Preview, Toggle Line Numbers, Word Wrap, Zoom, Theme
- **Format**: Heading 1-6, Bold, Italic, Strikethrough, Code, Quote, List, Link, Image, Table, HR, Math
- **Tools**: GitHub Sync, Settings, Extensions
- **Help**: About, Keyboard Shortcuts, Markdown Guide

#### 3.2 Format Commands (Apply to Selection) ✅ ALL IMPLEMENTED
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

#### 3.3 Context Menu ✅ COMPLETED
- [x] Right-click menu with format options (Undo, Redo, Cut, Copy, Paste, Bold, Italic, etc.)
- [x] "Copy as Markdown" / "Copy as HTML"

---

### Phase 4: File Operations (Week 4-5) 🔄 IN PROGRESS

#### 4.1 File Management ✅ COMPLETED
- [x] New File (Ctrl+N) - untitled, track dirty state
- [x] Open File (Ctrl+O) - OpenFileDialog, support .md, .markdown, .txt
- [x] Save (Ctrl+S) - Save to existing path
- [x] Save As (Ctrl+Shift+S) - SaveFileDialog
- [x] Export to HTML
- [x] Auto-save draft (periodic, recover on crash)
- [x] Recent Files menu (MRU list, max 10)
- [x] File encoding detection (UTF-8 default, fallback)

#### 4.2 Export Features ✅ COMPLETED
- [x] Export to HTML (with embedded CSS)
- [x] Export to PDF (via WebView2 print or PDF library)
- [ ] Export to Word (.docx) - optional
- [x] Copy as HTML to clipboard

#### 4.3 Drag & Drop ✅ COMPLETED
- [x] Drop .md files to open
- [x] Drop images to embed (copy to assets folder, insert link)
- [ ] Drag tabs to reorder (if tabbed interface) (N/A - no tabs)

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
- [x] Auto-complete for:
  - Markdown syntax (headings, lists, blockquotes, tables, code blocks)
  - Emoji (:smile:) with popup and mouse/keyboard selection
  - File paths (for links/images)
  - Line-start markdown markers
- [x] Bracket matching (highlight matching pairs with overlay)
- [x] Smart quotes/dashes (curly quotes/apostrophes, em dash via `---`, with toggle in Tools menu)
- [ ] Multiple cursors (Alt+Click) — deferred
- [x] Minimap (code overview) — scaled document overview with viewport indicator and click/drag scrolling

#### 6.1b View Toggles & Zoom ✅ COMPLETED
- [x] Show/Hide Preview pane (View menu + toolbar; persisted)
- [x] Show/Hide Editor pane (preview-only view) — mirrors the preview toggle; at least one of editor/preview always stays visible
- [x] Show/Hide Line Numbers, Minimap, Scroll Sync (View menu; persisted)
- [x] Zoom In / Zoom Out / Reset Zoom (Ctrl++ / Ctrl+- / Ctrl+0, also View menu + status bar combo) — scales editor font size, line-number gutter, and preview CSS zoom together; persisted and re-applied after preview reloads

#### 6.2 Preview Enhancements ✅ COMPLETED
- [x] Scroll sync between editor and preview (bidirectional)
- [x] Outline/TOC panel (collapsible) — Markdig heading tree (`IMarkdownParser.GetOutline`), click/Enter to jump editor + preview, current-section highlight, expand/collapse all, resizable, persisted (Ctrl+Shift+E)
- [x] Find in preview — find bar above the preview, highlights all matches, match count, next/prev (Enter/F3), match case; re-applied after live updates; hidden in print/PDF (Ctrl+Shift+F, Ctrl+F in preview)
- [x] Print preview — dedicated window using WebView2 browser print preview + Windows print dialog, print-friendly CSS (Ctrl+P)
- [x] Presentation mode (fullscreen preview) — borderless full-screen WebView2, larger type, live updates, opens at the current scroll position, Esc/F5 to exit (F5)

#### 6.3 Settings & Customization ✅ COMPLETED
- [x] Theme: Light / Dark / System (follows `AppsUseLightTheme`) — app-wide resource-dictionary swap (`Themes/Light.xaml`, `Themes/Dark.xaml`), dark title bar via `DwmSetWindowAttribute`, themed preview CSS; no custom color picker
- [x] Editor font family/size — curated font list + size picker in Settings; size is the base the zoom feature scales against
- [x] Preview CSS customization — custom CSS textbox in Settings, appended after the base/dark stylesheet in the live preview only (exported/copied HTML stays portable)
- [x] Keyboard shortcuts editor — rebind UI for the 10 commands that already have a real `KeyBinding` (Show Outline, Find, Replace, Go to Line, Find in Preview, Print Preview, Presentation Mode, Zoom In/Out/Reset), with conflict detection and reset-to-default; replaces the removed `CycleTheme` stub
- [x] Markdown flavor settings — checkboxes for Math (KaTeX), Diagrams (Mermaid), Emoji, Footnotes, Task Lists (`IMarkdownParser.UpdateOptions`); core GFM parsing always on, not a full CommonMark/GFM preset switcher
- [x] Auto-save interval — configurable (10s-2min), applied live to the existing auto-save timer
- [x] Line ending style (LF/CRLF) — applied on Save/Save As via `LineEndingNormalizer`; auto-save draft and HTML export stay unaffected

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