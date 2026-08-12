using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests.Updates;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.1.0", 0, 1, 0)]
    [InlineData("V1.2.3", 1, 2, 3)]
    [InlineData("0.10.2", 0, 10, 2)]
    public void ParsesTagsWithOrWithoutTheVPrefix(string tag, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(tag, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("0.1.0+a1028e1654e43bd8c99e769032eb820c95ab78bc", 0, 1, 0)]
    [InlineData("v1.4.2+deadbeef", 1, 4, 2)]
    [InlineData("0.1.0-rc1", 0, 1, 0)]
    public void StripsBuildMetadataAndPreReleaseSuffixesBeforeParsing(string tag, int major, int minor, int patch)
    {
        // AssemblyInformationalVersion gets "+<git-sha>" appended by the SDK by default
        // whenever the project sits in a git repository - the running app's own version
        // arrives in this shape, not as a bare tag.
        Assert.True(ReleaseVersion.TryParse(tag, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("v1.2")]
    [InlineData("v1.2.3.4")]
    [InlineData("v1.2.x")]
    public void RejectsAnythingThatIsNotThreeIntegers(string? tag)
    {
        Assert.False(ReleaseVersion.TryParse(tag, out _));
    }

    [Fact]
    public void NewerVersionComparesGreaterAcrossEveryComponent()
    {
        Assert.True(new ReleaseVersion(1, 0, 0).IsNewerThan(new ReleaseVersion(0, 9, 9)));
        Assert.True(new ReleaseVersion(0, 2, 0).IsNewerThan(new ReleaseVersion(0, 1, 9)));
        Assert.True(new ReleaseVersion(0, 1, 1).IsNewerThan(new ReleaseVersion(0, 1, 0)));
    }

    [Fact]
    public void EqualAndOlderVersionsAreNotNewer()
    {
        Assert.False(new ReleaseVersion(0, 1, 0).IsNewerThan(new ReleaseVersion(0, 1, 0)));
        Assert.False(new ReleaseVersion(0, 1, 0).IsNewerThan(new ReleaseVersion(0, 2, 0)));
    }
}
