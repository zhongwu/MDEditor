# MDEditor - AI Agent Rules

## Critical Rules for AI Agents

1. **NEVER commit or push without explicit user approval.** The user must say "push", "looks good, push", or similar explicit approval before any git commit or push operation.

2. **Always build and verify** changes before reporting to the user.

3. **Report results** and wait for user review before proceeding to commit/push.

4. **When the user reports an issue**, fix it, build, report results, and wait for approval before pushing.

5. **Never assume approval** - even if the user says "looks good", wait for explicit "push" instruction.

## Project Summary

- **Framework**: WPF (.NET 10)
- **Language**: C#
- **Markdown**: Markdig (GFM + extensions)
- **Preview**: WebView2
- **Architecture**: MVVM (CommunityToolkit.Mvvm)

## Key Files

- `MDEditor/MainWindow.xaml.cs` - Main window logic, editor/preview, file ops
- `MDEditor/MainWindow.xaml` - UI layout, toolbar, menus
- `MDEditor/ViewModels/MainWindowViewModel.cs` - Commands and callbacks
- `MDEditor.Core/Services/MarkdownParser.cs` - Markdig HTML conversion
- `MDEditor/FindReplaceDialog.cs` - Find/Replace dialog
- `MDEditor/GoToLineDialog.cs` - Go to Line dialog
- `DEVELOPMENT_PLAN.md` - Full roadmap and phase status
