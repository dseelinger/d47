using System.IO.Compression;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Storage;

public sealed class ADataBackupLeavesOutDownloadsAndLogsTests
{
    private static readonly DateTime At = new(2026, 10, 12, 9, 30, 15);

    private readonly AppPaths _paths = new(@"C:\d47-test");
    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void TheCommandersStateIsZippedAndTheLargeFoldersAreNot()
    {
        _files.WriteText(Path.Combine(_paths.Data, "story.json"), """{"commanders":[]}""");
        _files.WriteText(Path.Combine(_paths.Data, "settings.json"), "{}");
        _files.WriteText(Path.Combine(_paths.Data, "voices", "custom", "mine.json"), "{}");
        _files.WriteText(Path.Combine(_paths.Data, "models", "ggml-base.bin"), "model");
        _files.WriteText(Path.Combine(_paths.Data, "logs", "d47.log"), "log");
        _files.WriteText(Path.Combine(_paths.Data, "audio", "music", "combat", "track.mp3"), "music");
        _files.WriteText(Path.Combine(_paths.Data, "stories", "index.json"), "{}");
        _files.WriteBytes(Path.Combine(_paths.Backups, "data-0.109.7-local-20260908-130420.zip"), [1]);

        DataBackup.RunOnNewBuild(_paths, "0.111.0+abc", "0.111.0-debug", At, _files, NullLogger.Instance);

        using var zip = new ZipArchive(new MemoryStream(_files.ReadBytes(Path.Combine(_paths.Backups, "data-backup-20261012-093015-0.111.0-debug.zip"))!));

        Assert.Equal(
            ["settings.json", "story.json", "voices/custom/mine.json"],
            zip.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));

        using var story = new StreamReader(zip.GetEntry("story.json")!.Open());

        Assert.Equal("""{"commanders":[]}""", story.ReadToEnd());
    }
}
