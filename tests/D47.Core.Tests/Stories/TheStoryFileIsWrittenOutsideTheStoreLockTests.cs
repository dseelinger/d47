using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class TheStoryFileIsWrittenOutsideTheStoreLockTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly string _folder = Directory.CreateTempSubdirectory("d47-story-lock").FullName;
    private readonly ManualResetEventSlim _writing = new();
    private readonly ManualResetEventSlim _release = new();
    private int _writes;

    private string StoryPath => Path.Combine(_folder, "story.json");

    public void Dispose()
    {
        _release.Set();
        _writing.Dispose();
        _release.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static Story Picked(string id) => new() { Id = id, Title = id, PublicLayer = "-" };

    /// <summary>The first write signals <see cref="_writing"/> and waits for <see cref="_release"/>; later writes pass straight through.</summary>
    private StoryStore OpenWithAHeldFirstWrite() =>
        StoryStore.Open(StoryPath, NullLogger<StoryStore>.Instance, (path, contents) =>
        {
            if (Interlocked.Increment(ref _writes) == 1)
            {
                _writing.Set();
                _release.Wait(Patience);
            }

            AtomicFile.WriteAllText(path, contents);
        });

    private Task StartHeldWrite(Action write)
    {
        var task = Task.Run(write, TestContext.Current.CancellationToken);

        Assert.True(_writing.Wait(Patience), "the write never started");

        return task;
    }

    [Fact]
    public async Task AReaderIsNotHeldUpBySave()
    {
        var store = OpenWithAHeldFirstWrite();
        var saving = StartHeldWrite(() => store.Save("F1", Picked("a")));

        var read = Task.Run(() => store.For("F1"), TestContext.Current.CancellationToken);

        var result = await read.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.Equal("a", Assert.Single(result).Id);

        _release.Set();
        await saving;
    }

    [Fact]
    public async Task AReaderIsNotHeldUpByUpdate()
    {
        var store = OpenWithAHeldFirstWrite();
        Interlocked.Exchange(ref _writes, -1);
        store.Save("F1", Picked("a"));

        var updating = StartHeldWrite(() => store.Update("F1", "a", story => story with { Rating = 3 }));

        var read = Task.Run(() => store.Find("F1", "a"), TestContext.Current.CancellationToken);

        var result = await read.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.Equal(3, result!.Rating);

        _release.Set();
        await updating;
    }

    [Fact]
    public async Task AReaderIsNotHeldUpByMintingAVoter()
    {
        var store = OpenWithAHeldFirstWrite();
        var minting = StartHeldWrite(() => store.EnsureVoter("F1"));

        var read = Task.Run(() => store.Voter("F1"), TestContext.Current.CancellationToken);

        var result = await read.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.NotNull(result);

        _release.Set();
        await minting;
    }

    [Fact]
    public async Task TwoSavesAtOnceLeaveTheLaterListOnDisk()
    {
        var store = OpenWithAHeldFirstWrite();
        var first = StartHeldWrite(() => store.Save("F1", Picked("a")));
        var second = Task.Run(() => store.Save("F1", Picked("b")), TestContext.Current.CancellationToken);

        var sooner = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken));

        Assert.NotSame(second, sooner);

        _release.Set();
        await Task.WhenAll(first, second);

        var reopened = StoryStore.Open(StoryPath, NullLogger<StoryStore>.Instance);

        Assert.Equal(["a", "b"], reopened.For("F1").Select(story => story.Id));
    }
}
