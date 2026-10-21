---
name: mdeditor-dev
description: Development workflow and conventions for the MDEditor WPF Markdown editor project. Use when working on MDEditor features, fixing bugs, or understanding project structure.
allowed-tools: Read, Glob, Grep, Bash, Edit, Write
---

# MDEditor Development Workflow

## Project Overview

MDEditor is a WPF Markdown desktop editor (.NET 10) with dual-pane live preview, formatting toolbar, file operations, and syntax highlighting.

## Key Rules

1. **NEVER commit or push without explicit user approval.** The user must say "push" or "looks good, push".
2. **Always build and verify** changes before reporting to the user.
3. **Report results** and wait for user review before proceeding to commit/push.

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

## Tech Stack

- **UI Framework**: WPF (.NET 10)
- **Language**: C# (.NET 10)
- **Architecture**: MVVM (CommunityToolkit.Mvvm)
- **Markdown**: Markdig (GFM + extensions, KaTeX, Mermaid)
- **Preview**: WebView2 (HTML/CSS rendering)
- **Settings**: JSON in `%APPDATA%\MDEditor\settings.json`

## Build & Run

```bash
# Build
dotnet build MDEditor/MDEditor.csproj

# Run
dotnet run --project MDEditor/MDEditor.csproj

# Build core only
dotnet build MDEditor.Core/MDEditor.Core.csproj
```

## Key Files

- `MDEditor/MainWindow.xaml.cs` - Main window logic, editor/preview, file ops, scroll sync
- `MDEditor/MainWindow.xaml` - UI layout, toolbar, menus, context menu
- `MDEditor/ViewModels/MainWindowViewModel.cs` - Commands and callbacks
- `MDEditor.Core/Services/MarkdownParser.cs` - Markdig HTML conversion, KaTeX, Mermaid
- `MDEditor/FindReplaceDialog.cs` - Find/Replace dialog
- `MDEditor/GoToLineDialog.cs` - Go to Line dialog
- `DEVELOPMENT_PLAN.md` - Full roadmap and phase status

## Coding Conventions

- Use `CommunityToolkit.Mvvm` for ViewModels (RelayCommand, ObservableObject)
- Use callback pattern for UI operations (ViewModel → MainWindow via Action properties)
- WPF RichTextBox for editor, WebView2 for preview
- Settings via ISettingsService (JSON file-based)
- All dialog windows should be single-instance (track in MainWindow)

## Common Patterns

### Adding a new command:
1. Add `Action?` callback property to MainWindowViewModel
2. Add `[RelayCommand]` method that invokes the callback
3. Wire callback in MainWindow constructor
4. Add menu item and/or toolbar button in MainWindow.xaml

### Adding a new dialog:
1. Create dialog class inheriting from Window
2. Track instance in MainWindow (single-instance pattern)
3. Show via Show() with Owner = this
4. Clean up instance on Closed event

## Phase Status

- Phase 1: ✅ Complete
- Phase 2: ✅ Complete (Editor Core, Live Preview, Markdown Pipeline)
- Phase 3: ✅ Complete (Toolbar & Formatting, Context Menu)
- Phase 4: 🔄 In Progress (File Management done, auto-save/recent files pending)
- Phase 5: ⏳ Not Started (GitHub Integration)
- Phase 6: ⏳ Not Started (Advanced Features)
- Phase 7: ⏳ Not Started (Polish & Release)