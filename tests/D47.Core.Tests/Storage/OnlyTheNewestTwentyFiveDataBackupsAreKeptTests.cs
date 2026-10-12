using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Storage;

public sealed class OnlyTheNewestTwentyFiveDataBackupsAreKeptTests
{
    private static readonly DateTime First = new(2026, 10, 1, 9, 0, 0);

    private readonly AppPaths _paths = new(@"C:\d47-test");
    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void TheOldestBackupsPastTwentyFiveAreExpired()
    {
        var backups = Enumerable.Range(0, 30).Select(day => Path.Combine(_paths.Backups, DataBackup.FileName(First.AddDays(day), "0.111.0"))).ToList();

        var expired = DataBackup.Expired(backups);

        Assert.Equal(backups.Take(5), expired.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void AFileThatIsNotABackupIsNeverExpired()
    {
        var backups = Enumerable.Range(0, 30).Select(day => Path.Combine(_paths.Backups, DataBackup.FileName(First.AddDays(day), "0.111.0")));

        var expired = DataBackup.Expired(
            [.. backups, Path.Combine(_paths.Backups, "data-0.109.7-local-20260908-130420.zip"), Path.Combine(_paths.Backups, "notes.zip")]);

        Assert.Equal(5, expired.Count);
        Assert.All(expired, path => Assert.StartsWith("data-backup-2026", Path.GetFileName(path), StringComparison.Ordinal));
    }

    [Fact]
    public void ABackupAfterTwentyFiveDeletesTheOldest()
    {
        _files.WriteText(Path.Combine(_paths.Data, "story.json"), "{}");

        for (var day = 0; day < DataBackup.Kept; day++)
        {
            _files.WriteBytes(Path.Combine(_paths.Backups, DataBackup.FileName(First.AddDays(day), "0.111.0")), [1]);
        }

        var now = First.AddDays(DataBackup.Kept);

        DataBackup.RunOnNewBuild(_paths, "0.111.1+abc", "0.111.1", now, _files, NullLogger.Instance);

        var left = _files.Enumerate(_paths.Backups, "*");

        Assert.Equal(DataBackup.Kept, left.Count);
        Assert.DoesNotContain(Path.Combine(_paths.Backups, DataBackup.FileName(First, "0.111.0")), left, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(_paths.Backups, DataBackup.FileName(now, "0.111.1")), left, StringComparer.OrdinalIgnoreCase);
    }
}
