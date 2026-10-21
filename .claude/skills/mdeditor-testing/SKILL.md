---
name: mdeditor-testing
description: Testing strategy and patterns for MDEditor. Use when writing tests, running tests, or planning test coverage.
allowed-tools: Read, Glob, Grep, Bash, Edit, Write
---

# MDEditor Testing Strategy

## Test Projects

- `MDEditor.Tests` - Unit tests (xUnit + Moq)
- `MDEditor.IntegrationTests` - UI automation tests

## Test Framework

- **xUnit** - Test framework
- **Moq** - Mocking library
- **Microsoft.VisualStudio.TestPlatform** - Test runner

## Running Tests

```bash
dotnet test
dotnet test MDEditor.Tests/MDEditor.Tests.csproj
```

## What to Test

### Core Services (High Priority)
- `MarkdownParser.ToHtml()` - Markdown to HTML conversion
- `MarkdownParser.ToHtmlDocument()` - Full HTML document generation
- `SettingsService` - Get/Set/Save operations

### ViewModels (Medium Priority)
- Command execution (NewFile, OpenFile, SaveFile, etc.)
- Callback invocation
- State management (dirty flag, file path)

### UI Components (Lower Priority - Integration Tests)
- Find/Replace dialog behavior
- Go to Line navigation
- Line numbers display
- Syntax highlighting

## Test Patterns

### Unit Test (xUnit)
```csharp
[Fact]
public void ToHtml_WithHeading_ReturnsH1Tag()
{
    var parser = new MarkdownParser();
    var result = parser.ToHtml("# Hello");
    Assert.Contains("<h1>", result);
    Assert.Contains("Hello", result);
}
```

### Mocking Services
```csharp
var mockSettings = new Mock<ISettingsService>();
mockSettings.Setup(s => s.Get("key", "default")).Returns("value");
var vm = new MainWindowViewModel(mockSettings.Object, ...);
```

## Test Coverage Goals

- Core services: >80% coverage
- ViewModels: >70% coverage
- UI components: Critical paths only

## Performance Testing

- Large file loading (>10MB)
- Syntax highlighting on 100KB+ files
- Preview rendering latency (<300ms)