using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class TheStoryFileIsWrittenOutsideTheStoreLockTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly MemoryFileSystem _files = new();
    private readonly ManualResetEventSlim _writing = new();
    private readonly ManualResetEventSlim _release = new();
    private int _writes;

    private static string StoryPath => Path.Combine(@"C:\d47-test", "story.json");

    public void Dispose()
    {
        _release.Set();
        _writing.Dispose();
        _release.Dispose();
    }

    private static Story Picked(string id) => new() { Id = id, Title = id, PublicLayer = "-" };

    /// <summary>The first write signals <see cref="_writing"/> and waits for <see cref="_release"/>; later writes pass straight through.</summary>
    private StoryStore OpenWithAHeldFirstWrite() =>
        StoryStore.Open(StoryPath, new HeldWrites(_files, () =>
        {
            if (Interlocked.Increment(ref _writes) == 1)
            {
                _writing.Set();
                _release.Wait(Patience);
            }
        }), NullLogger<StoryStore>.Instance);

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

        var reopened = StoryStore.Open(StoryPath, _files, NullLogger<StoryStore>.Instance);

        Assert.Equal(["a", "b"], reopened.For("F1").Select(story => story.Id));
    }

    /// <summary>Runs <c>beforeWrite</c> ahead of each write to the wrapped file system.</summary>
    private sealed class HeldWrites(IFileSystem inner, Action beforeWrite) : IFileSystem
    {
        public FileState? Stat(string path) => inner.Stat(path);

        public DateTime? FolderWritten(string folder) => inner.FolderWritten(folder);

        public string? ReadText(string path) => inner.ReadText(path);

        public byte[]? ReadBytes(string path) => inner.ReadBytes(path);

        public Stream? OpenRead(string path) => inner.OpenRead(path);

        public void WriteBytes(string path, byte[] contents)
        {
            beforeWrite();
            inner.WriteBytes(path, contents);
        }

        public void WriteText(string path, string contents)
        {
            beforeWrite();
            inner.WriteText(path, contents);
        }

        public void AppendText(string path, string contents) => inner.AppendText(path, contents);

        public void CreateFolder(string folder) => inner.CreateFolder(folder);

        public void Delete(string path) => inner.Delete(path);

        public void Copy(string from, string to) => inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            inner.Enumerate(folder, pattern, recursive);
    }
}
