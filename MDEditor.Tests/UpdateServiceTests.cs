using MDEditor.Core.Services;
using Moq;
using Octokit;
using Octokit.Internal;

namespace MDEditor.Tests;

public class UpdateServiceTests
{
    private static Release MakeRelease(string tag, bool draft = false, bool prerelease = false) =>
        new SimpleJsonSerializer().Deserialize<Release>(
            $$"""{"tag_name":"{{tag}}","name":"MDEditor {{tag}}","body":"notes","draft":{{draft.ToString().ToLowerInvariant()}},"prerelease":{{prerelease.ToString().ToLowerInvariant()}},"html_url":"https://github.com/zhongwu/MDEditor/releases/tag/{{tag}}"}""");

    private static UpdateService ServiceReturning(Release release)
    {
        var releases = new Mock<IReleasesClient>();
        releases.Setup(r => r.GetLatest("zhongwu", "MDEditor")).ReturnsAsync(release);
        return new UpdateService(releases.Object);
    }

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("v0.10.1-beta.2", 0, 10, 1)]
    public void TryParseVersion_Valid(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateService.TryParseVersion(tag, out var v));
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nightly")]
    [InlineData("v1")]
    public void TryParseVersion_Invalid(string? tag) => Assert.False(UpdateService.TryParseVersion(tag, out _));

    [Fact]
    public async Task NewerRelease_IsReturned()
    {
        var info = await ServiceReturning(MakeRelease("v1.1.0")).CheckForUpdateAsync(new Version(1, 0, 0, 0));
        Assert.NotNull(info);
        Assert.Equal(new Version(1, 1, 0), info!.Version);
        Assert.Equal("notes", info.Notes);
        Assert.Contains("v1.1.0", info.HtmlUrl);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v0.9.9")]
    public async Task SameOrOlderRelease_ReturnsNull(string tag) =>
        Assert.Null(await ServiceReturning(MakeRelease(tag)).CheckForUpdateAsync(new Version(1, 0, 0, 0)));

    [Fact]
    public async Task TwoPartCurrentVersion_ComparesAsThreePart() =>
        Assert.Null(await ServiceReturning(MakeRelease("v1.0.0")).CheckForUpdateAsync(new Version(1, 0)));

    [Fact]
    public async Task DraftOrPrerelease_IsIgnored()
    {
        Assert.Null(await ServiceReturning(MakeRelease("v9.0.0", draft: true)).CheckForUpdateAsync(new Version(1, 0, 0)));
        Assert.Null(await ServiceReturning(MakeRelease("v9.0.0", prerelease: true)).CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Fact]
    public async Task UnparseableTag_ReturnsNull() =>
        Assert.Null(await ServiceReturning(MakeRelease("nightly")).CheckForUpdateAsync(new Version(1, 0, 0)));

    [Fact]
    public async Task NoReleasesYet_ReturnsNull()
    {
        var releases = new Mock<IReleasesClient>();
        releases.Setup(r => r.GetLatest(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("none", System.Net.HttpStatusCode.NotFound));
        Assert.Null(await new UpdateService(releases.Object).CheckForUpdateAsync(new Version(1, 0, 0)));
    }
}
