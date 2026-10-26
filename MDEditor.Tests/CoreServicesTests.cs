using System.Diagnostics;
using System.Text;
using MDEditor.Core;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Services;
using MDEditor.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MDEditor.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mdeditor-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "nested", "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Get_Missing_ReturnsDefault()
    {
        var s = new SettingsService(FilePath);
        Assert.Equal(42, s.Get("nope", 42));
    }

    [Fact]
    public void Set_PersistsAcrossInstances_ForAllCommonTypes()
    {
        var s = new SettingsService(FilePath);
        s.Set("a.int", 7);
        s.Set("a.str", "hello");
        s.Set("a.bool", true);
        s.Set("a.dbl", 1.5);

        var reloaded = new SettingsService(FilePath);
        Assert.Equal(7, reloaded.Get("a.int", 0));
        Assert.Equal("hello", reloaded.Get("a.str", ""));
        Assert.True(reloaded.Get("a.bool", false));
        Assert.Equal(1.5, reloaded.Get("a.dbl", 0.0));
    }

    [Fact]
    public void Get_InMemoryValue_ReturnsWithoutReload()
    {
        var s = new SettingsService(FilePath);
        s.Set("k", "v");
        Assert.Equal("v", s.Get("k", "x"));
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        var s = new SettingsService(FilePath);
        s.Set("k", "not a number");
        Assert.Equal(5, s.Get("k", 5));
        Assert.Equal(5, new SettingsService(FilePath).Get("k", 5));
    }

    [Fact]
    public void Remove_DeletesKey_AndPersists()
    {
        var s = new SettingsService(FilePath);
        s.Set("k", 1);
        s.Remove("k");
        Assert.Equal(-1, s.Get("k", -1));
        Assert.Equal(-1, new SettingsService(FilePath).Get("k", -1));
    }

    [Fact]
    public void Save_WritesFile()
    {
        var s = new SettingsService(FilePath);
        s.Save();
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void CorruptFile_StartsEmpty_AndRecovers()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ this is not json");
        var s = new SettingsService(FilePath);
        Assert.Equal("d", s.Get("k", "d"));
        s.Set("k", "v");
        Assert.Equal("v", new SettingsService(FilePath).Get("k", "d"));
    }

    [Fact]
    public void JsonNullValue_ReturnsDefault()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{\"k\": null}");
        Assert.Equal("d", new SettingsService(FilePath).Get("k", "d"));
    }
}

public class NavigationServiceTests
{
    private sealed class VmA : ViewModelBase { }
    private sealed class VmB : ViewModelBase { }

    [Fact]
    public void Initial_CannotGoBack_AndNoCurrent()
    {
        var n = new NavigationService();
        Assert.False(n.CanGoBack);
        Assert.Null(n.GetCurrentViewModel());
        n.GoBack(); // no-op
        Assert.Null(n.GetCurrentViewModel());
    }

    [Fact]
    public void NavigateAndBack_TracksStack()
    {
        var n = new NavigationService();
        n.NavigateTo<VmA>();
        Assert.False(n.CanGoBack);
        n.NavigateTo<VmB>();
        Assert.True(n.CanGoBack);
        Assert.Equal(nameof(VmB), n.GetCurrentViewModel());
        n.GoBack();
        Assert.Equal(nameof(VmA), n.GetCurrentViewModel());
        Assert.False(n.CanGoBack);
    }
}

public class LoggingServiceTests
{
    [Fact]
    public void ForwardsAllLevelsToLogger()
    {
        var logger = new Mock<ILogger<LoggingService>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var svc = new LoggingService(logger.Object);

        svc.LogInformation("i {0}", 1);
        svc.LogWarning("w {0}", 2);
        svc.LogError("e {0}", 3);
        svc.LogError(new InvalidOperationException("boom"), "ex {0}", 4);

        foreach (var level in new[] { LogLevel.Information, LogLevel.Warning, LogLevel.Error })
        {
            logger.Verify(l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), level == LogLevel.Error ? Times.Exactly(2) : Times.Once());
        }
    }
}

public partial class ViewModelBaseTests
{
    public sealed class Vm : ViewModelBase { }
    public sealed class ValidatableVm : ValidatableViewModelBase { }

    [Fact]
    public void BaseClasses_AreViewModels()
    {
        Assert.IsAssignableFrom<IViewModel>(new Vm());
        Assert.IsAssignableFrom<IViewModel>(new ValidatableVm());
    }
}

public class CoreServiceRegistrationTests
{
    [Fact]
    public void AddMDEditorCore_RegistersResolvableServices()
    {
        var services = new ServiceCollection();
        services.AddMDEditorCore();
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<ISettingsService>());
        Assert.NotNull(sp.GetRequiredService<INavigationService>());
        Assert.NotNull(sp.GetRequiredService<ILoggingService>());
        Assert.NotNull(sp.GetRequiredService<IMarkdownParser>());
        Assert.NotNull(sp.GetRequiredService<IGitHubAuthService>());
        Assert.NotNull(sp.GetRequiredService<IGitHubGistService>());
        Assert.NotNull(sp.GetRequiredService<IGitHubRepositoryService>());
    }
}


/// <summary>
/// Guards against pathological (quadratic or worse) behavior on very large documents.
/// Thresholds are deliberately generous so they only fail on real regressions, not slow CI machines.
/// </summary>
public class LargeDocumentPerformanceTests
{
    private static string BuildDocument(int targetBytes)
    {
        const string block =
            "# Heading\n\nSome *emphasis*, **strong** text with a [link](https://example.com) and `code`.\n\n" +
            "- item one\n- item two\n- [ ] task\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n```csharp\nvar x = 1;\n```\n\n";
        var sb = new StringBuilder(targetBytes + block.Length);
        // Unique headings: Markdig's auto-identifiers go quadratic on thousands of identical headings.
        for (var i = 0; sb.Length < targetBytes; i++) sb.Append(block.Replace("# Heading", "# Heading " + i));
        return sb.ToString();
    }

    [Fact]
    public void Parser_RendersTenMegabytes_InReasonableTime()
    {
        var doc = BuildDocument(10 * 1024 * 1024);
        var parser = new MarkdownParser();
        var sw = Stopwatch.StartNew();
        var html = parser.ToHtml(doc);
        sw.Stop();
        Assert.NotEmpty(html);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60), $"10 MB render took {sw.Elapsed}");
    }

    [Fact]
    public void TextStatistics_TenMegabytes_IsFast()
    {
        var doc = BuildDocument(10 * 1024 * 1024);
        var sw = Stopwatch.StartNew();
        var words = TextStatistics.CountWords(doc);
        sw.Stop();
        Assert.True(words > 0);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"word count took {sw.Elapsed}");
    }

    [Fact]
    public void LineEndingNormalizer_TenMegabytes_IsFast()
    {
        var doc = BuildDocument(10 * 1024 * 1024);
        var sw = Stopwatch.StartNew();
        var crlf = LineEndingNormalizer.Normalize(doc, "CRLF");
        sw.Stop();
        Assert.True(crlf.Length > doc.Length);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"normalize took {sw.Elapsed}");
    }
}
