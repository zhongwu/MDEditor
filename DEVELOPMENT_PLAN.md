# MDEditor Development Plan

## Project Overview
**MDEditor** - A modern Markdown desktop editor built with the latest WinUI platform (WinUI 3 / Windows App SDK), featuring a dual-pane interface with live preview, toolbar for formatting, file operations, and GitHub version synchronization.

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **UI Framework** | WinUI 3 (Windows App SDK 1.6+) |
| **Language** | C# (.NET 8/9) |
| **Markdown Parser** | Markdig (CommonMark + GitHub Flavored Markdown) |
| **Syntax Highlighting** | Markdig + WinUI RichTextBlock / WebView2 |
| **File Operations** | Windows.Storage APIs |
| **GitHub Integration** | Octokit.NET |
| **Settings/Config** | Windows.Storage.ApplicationData |
| **Architecture** | MVVM with CommunityToolkit.Mvvm |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Testing** | xUnit, Moq, Microsoft.VisualStudio.TestPlatform |

---

## Development Phases

### Phase 1: Foundation & Project Setup (Week 1-2)

#### 1.1 Project Initialization
- [ ] Create WinUI 3 Desktop project targeting .NET 8/9
- [ ] Configure Windows App SDK 1.6+ in project
- [ ] Set up solution structure:
  ```
  MDEditor.sln
  ├── MDEditor (WinUI 3 App)
  ├── MDEditor.Core (Class Library - Business Logic)
  ├── MDEditor.Tests (Unit Tests)
  └── MDEditor.IntegrationTests (UI Tests)
  ```
- [ ] Configure NuGet packages:
  - CommunityToolkit.Mvvm
  - CommunityToolkit.WinUI
  - Markdig
  - Octokit.NET
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging

#### 1.2 Core Architecture Setup
- [ ] Implement MVVM base classes (ObservableObject, ObservableValidator)
- [ ] Set up Dependency Injection container
- [ ] Configure logging (Serilog or Microsoft.Extensions.Logging)
- [ ] Create application settings service
- [ ] Implement navigation service (if multi-page needed)

#### 1.3 UI Shell
- [ ] Create main window with custom title bar (WinUI 3 style)
- [ ] Implement responsive two-pane layout (Grid with GridSplitter)
- [ ] Add toolbar with command bindings
- [ ] Implement status bar (line/column, encoding, word count)

---

#### Phase 1 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC1.1 | Solution builds and runs without errors | `dotnet build` succeeds; app launches |
| AC1.2 | Main window displays with custom title bar | Visual inspection; title bar draggable |
| AC1.3 | Two-pane layout with resizable splitter | Drag splitter; both panes resize |
| AC1.4 | Toolbar visible with placeholder commands | Toolbar renders; commands bound |
| AC1.5 | Status bar shows line/col, encoding, word count | Type in editor; values update |
| AC1.6 | DI container resolves all registered services | Unit test service resolution |
| AC1.7 | Settings service reads/writes JSON config | Integration test with temp config |

---

### Phase 2: Editor Core (Week 2-3)

#### 2.1 Markdown Text Editor (Left Pane)
- [ ] Use `RichEditBox` or `TextBox` with monospace font
- [ ] Implement syntax highlighting via Markdig + custom `TextHighlighter`
- [ ] Line numbers gutter
- [ ] Basic editing features:
  - [ ] Undo/Redo
  - [ ] Find/Replace (Ctrl+F, Ctrl+H)
  - [ ] Go to Line (Ctrl+G)
  - [ ] Word wrap toggle
  - [ ] Tab/Shift+Tab for indentation

#### 2.2 Live Preview (Right Pane)
- [ ] Option A: `WebView2` for full HTML rendering (recommended)
  - Load Markdig-rendered HTML with custom CSS
  - Support GitHub-style CSS (github-markdown-css)
  - Handle internal links, images, math (KaTeX), mermaid
- [ ] Option B: Native WinUI `RichTextBlock` with Markdig WinUI renderer
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
- [ ] Custom renderers for WinUI/WebView2

---

#### Phase 2 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC2.1 | Editor accepts text input with monospace font | Type text; font renders correctly |
| AC2.2 | Syntax highlighting for basic Markdown | Type `# Heading`, `**bold**`; colors apply |
| AC2.3 | Line numbers gutter visible and accurate | Scroll 50+ lines; numbers match |
| AC2.4 | Undo/Redo works for 50+ operations | Type, undo 10x, redo 10x; content restored |
| AC2.5 | Find/Replace finds all occurrences | Search "test" in 10 occurrences; all highlighted |
| AC2.6 | Go to Line navigates correctly | Ctrl+G → 42; cursor at line 42 |
| AC2.7 | Preview renders HTML from Markdig | Type markdown; preview shows HTML |
| AC2.8 | GitHub-style CSS applied in preview | Table, code block, blockquote styled |
| AC2.9 | Synchronized scrolling works | Scroll editor; preview scrolls proportionally |
| AC2.10 | Auto-refresh debounced at 300ms | Type rapidly; preview updates once |
| AC2.11 | Math formulas render via KaTeX | Type `$$E=mc^2$$`; renders in preview |
| AC2.12 | Mermaid diagrams render | Type mermaid block; diagram appears |
| AC2.13 | Markdig pipeline passes CommonMark spec | Run CommonMark test suite; >95% pass |

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

#### Phase 3 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC3.1 | All 6 menu categories present and functional | Click each menu; commands execute |
| AC3.2 | Format buttons apply markdown to selection | Select text → click Bold → `**text**` inserted |
| AC3.3 | Keyboard shortcuts match specification | Press Ctrl+B; bold applied |
| AC3.4 | Heading 1-6 shortcuts work (Ctrl+1..6) | Press Ctrl+3; `### ` inserted |
| AC3.5 | Format commands work with no selection | Cursor in middle of word; Ctrl+B wraps word |
| AC3.6 | Context menu appears on right-click | Right-click editor; menu shows format options |
| AC3.7 | Copy as HTML produces valid HTML | Copy selection; paste in browser; renders |
| AC3.8 | Toolbar state reflects selection (bold enabled when bold) | Select bold text; Bold button highlighted |
| AC3.9 | All format commands in table implemented | Test each row in Format Commands table |

---

### Phase 4: File Operations (Week 4-5)

#### 4.1 File Management
- [ ] New File (Ctrl+N) - untitled, track dirty state
- [ ] Open File (Ctrl+O) - FileOpenPicker, support .md, .markdown, .txt
- [ ] Save (Ctrl+S) - Save to existing path
- [ ] Save As (Ctrl+Shift+S) - FileSavePicker
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

#### Phase 4 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC4.1 | New File creates untitled document | Ctrl+N; tab shows "Untitled"; dirty=false |
| AC4.2 | Open File loads content correctly | Open .md file; content matches file |
| AC4.3 | Save persists changes to disk | Edit → Save → reopen; changes present |
| AC4.4 | Save As creates new file at chosen path | Save As → new path; file exists at path |
| AC4.5 | Dirty state tracked and shown in title | Edit → title shows `*`; Save → `*` removed |
| AC4.6 | Auto-save draft recovers on crash | Kill app → reopen → draft restored |
| AC4.7 | Recent Files lists last 10 opened | Open 12 files; menu shows 10 most recent |
| AC4.8 | UTF-8 files load without encoding errors | Open UTF-8 with emoji; displays correctly |
| AC4.9 | Export HTML includes embedded CSS | Export → open in browser; styles applied |
| AC4.10 | Export PDF produces valid PDF | Export → open in PDF reader; content matches |
| AC4.11 | Drag .md file onto window opens it | Drag file from Explorer; opens in editor |
| AC4.12 | Drop image copies to assets and inserts link | Drop PNG → `![image](assets/image.png)` inserted |

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

#### Phase 5 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC5.1 | OAuth flow completes and stores token | Authenticate → token in Credential Manager |
| AC5.2 | PAT authentication works | Enter PAT → repositories listed |
| AC5.3 | Token stored securely (not in plain text) | Check Credential Manager; encrypted |
| AC5.4 | Multi-account switch works | Add 2 accounts → switch → correct repos shown |
| AC5.5 | Repository browser lists user/org repos | Click GitHub → tree shows 50+ repos |
| AC5.6 | File tree navigation works | Expand folders → files listed |
| AC5.7 | Open from GitHub loads file in editor | Click file → editor shows content |
| AC5.8 | Save to GitHub creates commit | Edit → Sync → commit appears on GitHub |
| AC5.9 | Commit message prompt shown | Sync → dialog asks for message |
| AC5.10 | New branch/PR option works | Sync → choose new branch → PR created |
| AC5.11 | File history shows commits | History panel → list of commits with dates |
| AC5.12 | Diff view shows changes vs previous | Click commit → side-by-side diff |
| AC5.13 | Restore previous version works | Restore → editor content matches that version |
| AC5.14 | Gist create/update works (bonus) | Create gist → appears on gist.github.com |

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

#### Phase 6 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC6.1 | Markdown syntax autocomplete triggers on `#`, `*`, `[` | Type `#` → suggestions show headings |
| AC6.2 | Emoji autocomplete triggers on `:` | Type `:sm` → shows `:smile:` |
| AC6.3 | File path autocomplete for links/images | Type `](assets/` → lists files in assets |
| AC6.4 | Multiple cursors with Alt+Click | Alt+Click 3 lines → type → all 3 lines edited |
| AC6.5 | Minimap shows code overview | Open 200-line file → minimap visible |
| AC6.6 | Bracket matching highlights pairs | Cursor on `[` → matching `]` highlighted |
| AC6.7 | Smart quotes convert `"` to `"` and `"` | Type `"` → becomes `"` |
| AC6.8 | TOC panel shows headings hierarchically | Document with H1/H2/H3 → tree shows structure |
| AC6.9 | Click TOC entry scrolls to section | Click H2 in TOC → editor scrolls to H2 |
| AC6.10 | Find in preview highlights matches | Ctrl+F in preview → matches highlighted |
| AC6.11 | Print preview shows paginated output | File → Print Preview → pages render |
| AC6.12 | Presentation mode hides UI, fullscreen preview | View → Presentation → only preview visible |
| AC6.13 | Theme switch updates immediately | Settings → Dark → app theme changes |
| AC6.14 | Font family/size applies to editor | Settings → Consolas 16 → editor updates |
| AC6.15 | Custom preview CSS loads | Settings → custom CSS → preview uses it |
| AC6.16 | Keyboard shortcuts editor remaps keys | Remap Ctrl+S → new shortcut works |
| AC6.17 | Markdown flavor changes parser behavior | Switch to CommonMark → GFM features disabled |
| AC6.18 | Auto-save interval configurable | Set 60s → wait → draft saved at 60s |

---

### Phase 7: Polish & Release (Week 7-8)

#### 7.1 Quality Assurance
- [ ] Unit tests for core services (>80% coverage)
- [ ] UI automation tests for critical flows
- [ ] Performance profiling (large files >10MB)
- [ ] Memory leak detection
- [ ] Accessibility (Narrator, high contrast, keyboard nav)

#### 7.2 Packaging & Deployment
- [ ] MSIX packaging with Windows App SDK
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

#### Phase 7 Acceptance Criteria
| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC7.1 | Unit test coverage >80% for core services | `dotnet test --collect:"Code Coverage"` |
| AC7.2 | UI automation tests cover critical flows | Run integration tests; all pass |
| AC7.3 | Large file (10MB) opens in <3s | Open 10MB .md; stopwatch <3s |
| AC7.4 | Memory stable after 1hr editing | Profile; no leak >50MB growth |
| AC7.5 | Narrator reads editor content | Enable Narrator; navigate editor |
| AC7.6 | High contrast mode works | System HC theme; all UI visible |
| AC7.7 | Full keyboard navigation (no mouse) | Tab through all controls; all reachable |
| AC7.8 | MSIX package installs on clean Windows | Install MSIX; app runs |
| AC7.9 | Code-signed package passes SmartScreen | Sign → download → no warning |
| AC7.10 | Auto-update checks GitHub Releases | New version → app notifies |
| AC7.11 | Microsoft Store submission passes (optional) | Submit → certification passes |
| AC7.12 | GitHub Release with changelog auto-generated | Tag release → GH Release created |
| AC7.13 | User guide accessible from Help menu | Help → User Guide → opens |
| AC7.14 | Keyboard shortcuts reference complete | Help → Shortcuts → all shortcuts listed |
| AC7.15 | CONTRIBUTING.md guides new contributors | New contributor follows guide; PR accepted |

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

### UI Component Structure

```
MainWindow
├── TitleBar (Custom)
├── MenuBar / CommandBar
├── Toolbar (Format buttons)
├── MainContent (Grid)
│   ├── EditorPane (Left)
│   │   ├── LineNumberGutter
│   │   └── MarkdownEditor (RichEditBox/TextBox)
│   ├── GridSplitter
│   └── PreviewPane (Right)
│       └── MarkdownPreview (WebView2 / RichTextBlock)
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
| WinUI 3/WebView2 stability issues | Medium | High | Test on target Windows versions; fallback to RichTextBlock |
| Large file performance (>10MB) | Medium | Medium | Virtualized editor, lazy loading, background parsing |
| GitHub API rate limits | Medium | Medium | Implement caching, conditional requests, user feedback |
| Markdig rendering differences from GitHub | Low | Medium | Test with GitHub samples; allow custom CSS |
| Windows App SDK versioning | Low | High | Pin SDK version; test updates in CI |

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
- [ ] Mobile companion app (WinUI 3 mobile)

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
dotnet new winui3 -n MDEditor -o MDEditor
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
dotnet add MDEditor package Microsoft.WindowsAppSDK
dotnet add MDEditor package CommunityToolkit.WinUI
dotnet add MDEditor package Microsoft.Web.WebView2

# Build and run
dotnet build
dotnet run --project MDEditor/MDEditor.csproj
```

---

*Document Version: 1.0*  
*Created: 2026*  
*Last Updated: 2026*