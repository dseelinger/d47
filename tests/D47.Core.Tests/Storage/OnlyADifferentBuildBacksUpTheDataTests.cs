using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Storage;

public sealed class OnlyADifferentBuildBacksUpTheDataTests
{
    private static readonly DateTime At = new(2026, 10, 12, 9, 30, 15);

    private readonly AppPaths _paths = new(@"C:\d47-test");
    private readonly MemoryFileSystem _files = new();

    public OnlyADifferentBuildBacksUpTheDataTests() =>
        _files.WriteText(Path.Combine(_paths.Data, "story.json"), "{}");

    private int Backups => _files.Enumerate(_paths.Backups, "*.zip").Count;

    [Fact]
    public void TheFirstStartWithNoBuildRecordedBacksUp()
    {
        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc", "0.111.0", At, _files, NullLogger.Instance);

        Assert.Equal(1, Backups);
        Assert.Equal("0.111.0+abc", _files.ReadText(_paths.LastBuildFile));
    }

    [Fact]
    public void TheSameBuildStartingAgainTakesNoBackup()
    {
        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc", "0.111.0", At, _files, NullLogger.Instance);
        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc", "0.111.0", At.AddHours(1), _files, NullLogger.Instance);

        Assert.Equal(1, Backups);
    }

    [Fact]
    public void ARebuildAtTheSameVersionBacksUpAgain()
    {
        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc 2026-10-12T09:00:00", "0.111.0-debug", At, _files, NullLogger.Instance);
        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc 2026-10-12T09:45:00", "0.111.0-debug", At.AddHours(1), _files, NullLogger.Instance);

        Assert.Equal(2, Backups);
        Assert.Equal("0.111.0+abc 2026-10-12T09:45:00", _files.ReadText(_paths.LastBuildFile));
    }

    [Fact]
    public void AnEmptyDataFolderLeavesNoZip()
    {
        var empty = new AppPaths(@"C:\d47-empty");

        DataBackup.RunOnNewBuild(empty, "0.111.0+abc", "0.111.0", At, _files, NullLogger.Instance);

        Assert.Empty(_files.Enumerate(empty.Backups, "*"));
        Assert.Equal("0.111.0+abc", _files.ReadText(empty.LastBuildFile));
    }
}
