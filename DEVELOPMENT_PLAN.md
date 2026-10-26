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
- **Help**: About, Markdown Guide (keyboard shortcuts are shown/configured in Settings → Keyboard Shortcuts instead of a separate Help item)

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

### Phase 4: File Operations (Week 4-5) ✅ COMPLETED

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

### Phase 5: GitHub Integration (Week 5-6) ✅ COMPLETED

#### 5.1 Authentication ✅ COMPLETED
- [x] GitHub OAuth device flow (Octokit) — one-time code copied to clipboard + browser opened, polls until approved/denied/expired, cancellable; requests `repo gist read:org`. Uses MDEditor's registered OAuth App (`GitHubAuthService.DefaultOAuthClientId`, public client ID, no secret); overridable under Advanced in the GitHub Accounts window
- [x] Personal Access Token (PAT) support — validated against `GET /user` before saving; classic and fine-grained tokens
- [x] Secure token storage — Windows Credential Manager (`CredentialManagerTokenStore`, targets `MDEditor:github:<login>`); only non-secret account metadata goes to settings.json
- [x] Multi-account support — Tools > GitHub Accounts... (add / set active / remove), active account shown in the status bar; `IGitHubAuthService.CreateClient()` hands authenticated clients to 5.2+

#### 5.2 Repository Operations ✅ COMPLETED
- [x] Browse repositories (list user/org repos) — File > Open from... > GitHub repository: filterable list (owned, collaborator, org), private/read-only markers, remembers the last repo
- [x] Browse files in repository (tree view) — branch picker (default first), recursive tree with "Markdown files only" filter (folders without Markdown pruned), truncation warning for huge repos
- [x] Open file from GitHub — downloaded straight into the editor (no temp file); title shows `file (owner/repo@branch)`; branch head commit pinned at open
- [x] Save/Sync back to GitHub (Save, or File > Commit... / Ctrl+Shift+Enter):
  - [x] Commit changes with message (+ optional description); preserves the file's original line endings
  - [x] Create new branch / PR option — branch created from the opened commit, optional PR into the original branch; later saves follow the new branch
  - [x] Handle merge conflicts (basic) — stale SHA detected (409/422) → choose: commit to new branch (recommended), overwrite theirs, or discard mine and load theirs
- Save As detaches the document from GitHub (becomes a local file); read-only repos explain that Save As is the way to keep changes

#### 5.3 Version History ✅ COMPLETED
- [x] View commit history for current file — File > History...: last 100 commits on the branch that touched the file (summary, author, date, short SHA, full message tooltip), View Commit on GitHub
- [x] Compare with previous version (diff view) — built-in Myers line diff (`LineDiff`, no extra package): "Changes in this commit" (vs. parent) or "This version vs. my current text"; +/- stats, old/new line numbers, unchanged runs collapsed with 3 lines of context (or "Show whole file"), virtualized list
- [x] Restore previous version — loads the version into the editor (marked unsaved, confirms before discarding edits); the next save commits it with a pre-filled "Restore <file> to <sha>" message, so history is never rewritten

#### 5.4 Gist Support (Bonus) ✅ COMPLETED
- [x] Create/Update secret/public gists — File > Save As... > GitHub Gist (file name, optional description, secret by default; link copied to clipboard, offer to open); Save on a gist document updates it in place, preserving line endings. Gists have no per-file SHA, so a newer `updated_at` on GitHub prompts overwrite / load latest / keep editing. Missing `gist` scope on a PAT gets an explanatory error
- [x] Open gist files — File > Open from... > GitHub Gist: your gists (filterable, public/secret marker, file count, updated date) or any gist by URL/ID (pre-filled from a gist link on the clipboard); pick a file (Markdown first). Other people's gists open read-only (Save explains Save as Gist / Save As); File > Open Gist in Browser while a gist is open

#### 5.6 File Menu & Source Organization ✅ COMPLETED
Local, GitHub and gist documents had grown into scattered menu entries, with Commit and History hidden under Tools. The File menu now groups by source and keeps document actions together:
- [x] **Open from...** — one menu item that opens a small chooser (GitHub repository / GitHub Gist); plain Open... / Ctrl+O stays the direct local-file entry. Gitee and other hosts become another chooser option
- [x] **Save As...** (Ctrl+Shift+S) — opens the same kind of chooser (This computer / GitHub Gist); the separate Save as Gist item is gone. Save on an untitled document still goes straight to the local dialog
- [x] **Commit...** (Ctrl+Shift+Enter) moved from Tools to File, next to Save; shown only for GitHub repository documents
- [x] **One History... entry** replacing the separate File History (GitHub) and Gist History items; opens the repository commit history or the gist revision history depending on the open document, shown for any GitHub-hosted document
- [x] Open Gist in Browser moved next to History; Tools keeps GitHub Accounts, Smart Typography and Settings
- [x] Labels made host-neutral (Commit..., History...) so Gitee can reuse them (see 5.5); localized in en / zh-Hans / zh-Hant
- [ ] Follow-ups: add a GitHub repository (and Gitee) option to the Save As... chooser; move GitHub Accounts under File or Settings; "Save (commit)" wording for GitHub documents; show repo · branch · path in the title/status bar
#### 5.5 Gitee Support (Planned — post-v0.3.0)
Goal: let users in China (where github.com can be slow or blocked) open, edit and sync Markdown files on [Gitee](https://gitee.com) like they do on GitHub. Today every GitHub feature is hardcoded to github.com via Octokit.NET (`GitHubAuthService`, `GitHubRepositoryService`, `GitHubGistService`, and `https://github.com/...` URLs in the models), so this needs a provider abstraction first.
- [ ] Provider abstraction: introduce provider-neutral interfaces (auth, repository, file history) that the existing GitHub services implement, and make the UI/ViewModels depend on them instead of GitHub types; keep gists GitHub-only (Gitee has no equivalent)
- [ ] Provider selection: GitHub / Gitee choice in the account and repository windows; account model gains a provider and host; menu labels and tooltips stop saying "GitHub" when the active document is on Gitee
- [ ] Gitee authentication: personal access token (Gitee OAuth device flow is not available), stored in Windows Credential Manager like GitHub tokens; multi-account like GitHub
- [ ] Gitee repository operations via Gitee REST API v5 (`https://gitee.com/api/v5`): list repos/branches/tree, open file, save/commit with message, create branch + pull request, conflict detection via file `sha`
- [ ] Gitee file history: commit list for a file, view a version, diff vs parent / vs current, restore — reusing `GitHubHistoryWindow` and `LineDiff`
- [ ] Remaining: Save As... to GitLab (create a new file in a project), private-project images in the preview (needs the PRIVATE-TOKEN header on raw URLs), a visible warning for plain-HTTP servers.: commit/blob/PR URLs and "open in browser" built per provider (Gitee uses `gitee.com/{owner}/{repo}/blob/{branch}/{path}`)
- [ ] Image URL rewriting for relative images on Gitee (raw URLs differ from GitHub's)
- [ ] Settings: optional custom host (enables GitHub Enterprise Server and self-hosted Gitee later; GitLab gets this in 5.7)
- [ ] Localization (en / zh-Hans / zh-Hant) for all new strings; unit tests with a mocked HTTP layer
- Open questions: Gitee API rate limits and token scopes; whether to use `HttpClient` directly or a community SDK; update checks stay on GitHub Releases
#### 5.7 GitLab Support, including self-hosted (🔄 IN PROGRESS — open / commit / history work; polish and live testing remain)
Goal: open, edit and sync Markdown files on GitLab, both gitlab.com and self-hosted servers (for example a team instance at a custom URL and port). Builds on the provider abstraction from 5.5, so the File menu choosers (Open from... / Save As...) gain a GitLab option without new menu items.
- [ ] GitLab provider behind the provider-neutral interfaces from 5.5; reuse the same document, commit and history UI. **Status:** implemented first as its own services (`IGitLabAuthService`, `IGitLabRepositoryService`, `GitLab*` models; only the neutral tree/commit/conflict types are shared with GitHub) so GitHub is untouched; unifying both behind one provider interface is still the 5.5 refactor
- [x] **Server URL setting** per account (default `https://gitlab.com`): accepts `http(s)://host[:port]` for self-hosted instances; connection test that checks `GET /api/v4/version` and reports unreachable / wrong server / not authorized clearly
- [x] Authentication (Core + Tools > GitLab Accounts... window): personal access token (scopes `api` or `read_api` + `write_repository`), stored in Windows Credential Manager per server + account; multi-account like GitHub
- [x] Project browsing and file operations **service layer** via GitLab REST API v4 (projects, branches, recursive tree, read file, commit with conflict detection, new branch + merge request; unit-tested against a scripted fake server). **UI done:** File > Open from... > GitLab project (project/branch/file browser), Save / Commit... (commit dialog with new branch + merge request, conflict dialog with new-branch / overwrite / load-theirs), History... (diff and restore). Not yet exercised against a real server. Original scope: (`/api/v4`): list projects the user is a member of (project IDs / URL-encoded `namespace/path`), branches, repository tree, read file, create/update file with commit message, create branch + merge request; optimistic concurrency via `last_commit_id` / blob SHA
- [x] File history **service layer** (`repository/commits?path=`, file at commit); **history window done**: for a file, view a version at a commit, diff vs parent / vs current, restore — reusing the history window and `LineDiff`
- [ ] Snippets as the gist equivalent (optional): open / save / update personal snippets
- [ ] Remaining: Save As... to GitLab (create a new file in a project), private-project images in the preview (needs the PRIVATE-TOKEN header on raw URLs), a visible warning for plain-HTTP servers. ("open in browser" for blob, commit and merge request URLs use GitLab's `/-/blob/`, `/-/commit/` form) and relative-image rewriting for GitLab raw URLs
- [ ] Self-hosted details: custom ports, plain http:// and a per-account "accept untrusted certificate" option are supported; a visible warning for plain-HTTP servers is still to do; no hardcoded gitlab.com in the services
- [ ] Localization (en / zh-Hans / zh-Hant) and unit tests against a mocked HTTP layer
- Open questions: GitLab SDK (GitLabApiClient / NGitLab) versus a small `HttpClient` wrapper shared with Gitee; token scopes across GitLab versions; rate limits on self-hosted servers
- Test target: the self-hosted instance used in development is a standard GitLab (sign-in at `/users/sign_in`, API v4)
---

### Phase 6: Advanced Features (Week 6-7) ✅ COMPLETED

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

#### 6.4 Multi-Language Support (UI Localization) ✅ MOSTLY COMPLETED
- [x] String externalization — ~350 user-visible strings (menus, toolbar tooltips, context menu, dialogs, message boxes, file-dialog filters, status bar, Settings, find/go-to bars, print/presentation windows, GitHub windows, GitHub service errors, snippet labels) moved to `MDEditor.Core/Resources/Strings*.resx`. Source of truth is `MDEditor.Core/Resources/strings.tsv` (key / en / zh-Hans / zh-Hant); `tools/Generate-Strings.ps1` regenerates the .resx files. Log messages, default commit/PR messages and inserted Markdown stay English
- [x] Localization service — static `Loc` (Core: `Get`/`Format`, `Culture`, `SupportedLanguages`, `LanguageChanged`) plus the `{loc:Tr Key}` markup extension (binds to an indexer source, so XAML text updates live); code-built text (title bar, status bar, recent files, accounts list, shortcut rows) refreshes on `LanguageChanged`
- [x] Language picker in Settings (Appearance tab) — "Match Windows" (default) / English / 简体中文 / 繁體中文 (native names); persisted as `appearance.language`; applied live, no restart
- [x] Initial languages — English, Simplified Chinese (zh-Hans), Traditional Chinese (zh-Hant). Windows zh-CN/SG map to zh-Hans, zh-TW/HK/MO to zh-Hant, everything else to English; missing keys fall back to English
- [x] Culture-aware formatting — history dates and device-code expiry time use the UI culture; only the UI culture changes (number/date parsing and file content stay culture-invariant)
- [ ] Keyboard shortcut display — not needed for Chinese (Windows uses Ctrl/Shift/Alt); revisit for German etc.
- [ ] Layout robustness — Chinese is shorter than English, so no clipping expected; pseudo-localization check still to do before adding longer languages
- [x] Localized bundled content — Help → Markdown Guide (syntax reference rendered through the app's own Markdown pipeline, so it matches the real preview and theme) and Help → About, both fully localized (not just menu chrome) via `MarkdownGuide_Content`/`About_Tagline`/`About_Version` resource keys; re-renders live on a language switch while open. Help → Keyboard Shortcuts was dropped — Settings → Keyboard Shortcuts already covers it with live, rebindable gestures
- [ ] Right-to-left languages (stretch)
- [x] Tests — every key present and non-empty in each translation with matching `{n}` placeholders, translations not copies of English, every key used in source exists, no unused keys, culture mapping, live switching of a real XAML dialog
- Known limitation: CJK font fallback (YaHei vs JhengHei) is chosen at startup, so after a live switch between Simplified and Traditional the glyph style updates on the next launch

---

### Phase 7: Polish & Release (Week 7-8) 🔄 IN PROGRESS — v0.3.0 released

#### 7.1 Quality Assurance
- [x] Unit tests for core services (>80% coverage) — MDEditor.Core line coverage ~87%
- [ ] UI automation tests for critical flows
- [x] Performance profiling (large files >10MB) — regression-guard tests render/count/normalize 10 MB (note: thousands of identical headings make Markdig auto-ids quadratic)
- [ ] Memory leak detection
- [ ] Accessibility (Narrator, high contrast, keyboard nav) — partial: icon-only buttons and main panes now have AutomationProperties.Name; high contrast / manual Narrator pass still open

#### 7.2 Packaging & Deployment
- [x] MSIX packaging with WPF — `tools/Build-Msix.ps1` (manifest in `MDEditor/Packaging/`), x64 + ARM64, plus portable zip
- [ ] Code signing certificate — pipeline ready (script + release workflow sign when `WINDOWS_CERT_*` secrets exist); a real certificate still has to be purchased/provided
- [x] Auto-update (MSIX auto-update or GitHub Releases) — Help → Check for Updates against the latest GitHub release; installs are manual (opens the download page)
- [ ] Microsoft Store submission (optional)
- [x] GitHub Releases with changelog — `CHANGELOG.md` + tag-triggered `.github/workflows/release.yml`. **v0.3.0 published**: https://github.com/zhongwu/MDEditor/releases/tag/v0.3.0 (portable zips x64/ARM64; MSIX unsigned until signing secrets are added; see `Release.md` for the procedure)

#### 7.3 Documentation
- [x] User guide (Markdown, bundled in app) — Help → Markdown Guide, localized (see 6.4)
- [ ] Keyboard shortcuts reference (standalone doc) — not planned; Settings → Keyboard Shortcuts already shows/edits every rebindable gesture live
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
- [ ] Other Git hosts beyond GitHub/Gitee/GitLab (Azure DevOps, Bitbucket) — builds on the provider abstraction in 5.5 and the File menu structure in 5.6

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
