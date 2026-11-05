using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class TheRetiredBenchSlotsAreDeletedAtStartupTests
{
    private static readonly string Data = Path.Combine(MemoryInstall.FakeRoot, "slots", "data");
    private static readonly string Bench = Path.Combine(Data, StoryBench.Folder);

    private readonly MemoryFileSystem _files = new();

    private string Put(params string[] parts)
    {
        var path = Path.Combine([Bench, .. parts]);

        _files.CreateFolder(Bench);

        _files.WriteText(path, "x");
        return path;
    }

    [Fact]
    public void OnlyTheCheckpointsRemain()
    {
        var one = Put("one", "story.json");
        var oneCheckpoint = Put("one", "checkpoints", "1", "checkpoint.json");
        var two = Put("two", "adventures.json");
        var state = Put("bench.json");
        var kept = Put("mine", "checkpoints", "2", "checkpoint.json");
        var keptStory = Put("mine", "checkpoints", "2", "story.json");
        var mine = Path.Combine(Data, "story.json");

        _files.WriteText(mine, "x");

        StoryBench.RemoveSlots(Data, _files);

        Assert.Null(_files.Stat(one));
        Assert.Null(_files.Stat(oneCheckpoint));
        Assert.Null(_files.Stat(two));
        Assert.Null(_files.Stat(state));
        Assert.NotNull(_files.Stat(kept));
        Assert.NotNull(_files.Stat(keptStory));
        Assert.NotNull(_files.Stat(mine));
    }

    [Fact]
    public void AnEmptySlotNamedInBenchJsonIsDeletedToo()
    {
        _files.CreateFolder(Path.Combine(Bench, "empty"));
        _files.WriteText(Path.Combine(Bench, "bench.json"), "{\"slots\":[\"empty\"]}");

        StoryBench.RemoveSlots(Data, _files);

        Assert.Null(_files.Stat(Path.Combine(Bench, "bench.json")));
        Assert.Null(_files.FolderWritten(Path.Combine(Bench, "empty")));
    }

    [Fact]
    public void ADataFolderWithNoBenchIsLeftAlone()
    {
        var mine = Path.Combine(Data, "story.json");

        _files.WriteText(mine, "x");

        StoryBench.RemoveSlots(Data, _files);

        Assert.NotNull(_files.Stat(mine));
    }
}
