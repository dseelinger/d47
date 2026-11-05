using System.Text;
using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A run that has no record gets one filled once from the lines already in messages.json and data\messages.</summary>
public sealed class StoryRunRecordsAreFilledFromExistingLinesTests
{
    private const string Commander = "F1";

    private const string StoryId = "the-test-story";

    private const string Data = @"C:\d47-test\data";

    private const string Folder = Data + @"\story-runs";

    private const string ClipFolder = Data + @"\messages";

    private static readonly DateTimeOffset Picked = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private readonly StoryStore _stories = StoryStore.InMemory();

    private readonly MessageClips _clips;

    private readonly MessageStore _messages;

    private readonly StoryRunRecord _record;

    public StoryRunRecordsAreFilledFromExistingLinesTests()
    {
        _clips = new MessageClips(_files, ClipFolder, new ReversibleProtector());
        _messages = new MessageStore(Data + @"\messages.json", _files, NullLogger<MessageStore>.Instance, _clips);
        _record = new StoryRunRecord(_files, Folder, _stories, () => Commander, new TagCompressor(), new ReversibleProtector(), NullLogger.Instance);
    }

    private static Story Run(DateTimeOffset at, StoryState state, params string[] chapters) => new()
    {
        Id = StoryId,
        Title = "The Test Story",
        PublicLayer = "A test.",
        PickedAt = at,
        State = state,
        Chapters = chapters,
    };

    private static SpokenClip Spoken(string? voiceId = "voice-a") =>
        new([new AudioClip("line", new byte[960], AudioFormat.Standard)], "kokoro", voiceId);

    private int Fill() => _record.Backfill(_messages.All, StoryRunRecord.StoredClips(_clips));

    private string RunFolder(Story run) => Path.Combine(Folder, StoryRunRecord.FolderName(Commander, run.Id, run.PickedAt));

    private Dictionary<string, byte[]> Files(bool inRecord) => _files.Enumerate(Data, "*", recursive: true)
        .Where(path => path.StartsWith(Folder, StringComparison.OrdinalIgnoreCase) == inRecord)
        .ToDictionary(path => path, path => _files.ReadBytes(path)!);

    [Fact]
    public void EachRunGetsItsOwnLinesWithAndWithoutClipsInTheirOriginalOrder()
    {
        var first = Run(Picked, StoryState.Abandoned, "chapter-one");
        var second = Run(Picked.AddDays(3), StoryState.Running, "chapter-two");
        _stories.Save(Commander, first);
        _stories.Save(Commander, second);

        var a = _messages.Post("Ada Reyes", "Arrival", "First.", Picked.AddDays(1), "chapter-one", spoken: Spoken(), cast: "the-test-story.ada");
        _messages.Post("narrator", "Arrival", "Second.", Picked.AddDays(1).AddSeconds(5), "chapter-one");
        _messages.Post("narrator", "Later", "Third.", Picked.AddDays(4), "chapter-two", spoken: Spoken());

        Assert.Equal(2, Fill());

        var firstLines = _record.Read(Commander, first);
        Assert.Equal(["First.", "Second."], firstLines.Select(line => line.Body));
        Assert.Equal(a.Sent, firstLines[0].Sent);
        Assert.Equal("the-test-story.ada", firstLines[0].Cast);
        Assert.Equal(a.Key + ".m4a", firstLines[0].Clip);
        Assert.Equal(new MessageVoice("kokoro", "voice-a"), firstLines[0].Voice);
        Assert.Null(firstLines[1].Clip);
        Assert.Equal("Third.", Assert.Single(_record.Read(Commander, second)).Body);
    }

    [Fact]
    public void ALineWhoseClipFileIsMissingIsRecordedWithoutOne()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var posted = _messages.Post("narrator", "Arrival", "No file.", Picked.AddDays(1), "chapter-one", spoken: Spoken());
        _files.Delete(Path.Combine(ClipFolder, posted.Clip!));

        Fill();

        var line = Assert.Single(_record.Read(Commander, run));
        Assert.Equal(("No file.", null, null), (line.Body, line.Clip, line.Voice));
    }

    [Fact]
    public void AClipInTheCommandersOwnVoiceStaysProtected()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var posted = _messages.Post("ship-ai", "Arrival", "In my voice.", Picked.AddDays(1), "chapter-one", spoken: Spoken(OwnVoice.VoiceId));

        Fill();

        var line = Assert.Single(_record.Read(Commander, run));
        Assert.Equal(posted.Key + ".m4a" + MessageClips.ProtectedExtension, line.Clip);
        Assert.True(new ReversibleProtector().TryUnprotect(_files.ReadBytes(Path.Combine(RunFolder(run), line.Clip!))!, out var plain));
        Assert.Equal("M4A!", Encoding.ASCII.GetString(plain, 0, 4));
    }

    [Fact]
    public void ShortStoryLinesAndLinesTiedToNoStoryAreNotCopied()
    {
        _stories.Save(Commander, Run(Picked, StoryState.Running, "chapter-one"));
        _messages.Post("narrator", "A short story", "A beat.", Picked.AddDays(1), "short-story-key", spoken: Spoken());
        _messages.Post("ship-ai", "Hello", "Nothing to do with a story.", Picked.AddDays(1));

        Assert.Equal(0, Fill());
        Assert.Empty(Files(inRecord: true));
    }

    [Fact]
    public void ASecondStartWritesNothingAndLeavesAnExistingRecordAlone()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        _messages.Post("narrator", "Arrival", "First.", Picked.AddDays(1), "chapter-one", spoken: Spoken());
        Assert.Equal(1, Fill());
        var before = Files(inRecord: true);

        _messages.Post("narrator", "Arrival", "Posted after.", Picked.AddDays(2), "chapter-one");

        Assert.Equal(0, Fill());
        var after = Files(inRecord: true);
        Assert.Equal(before.Keys, after.Keys);
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    [Fact]
    public void FillingLeavesTheMessagesAndTheirClipsUnchanged()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        _messages.Post("narrator", "Arrival", "First.", Picked.AddDays(1), "chapter-one", spoken: Spoken());
        var before = Files(inRecord: false);

        Fill();

        var after = Files(inRecord: false);
        Assert.Equal(before.Keys, after.Keys);
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    private sealed class TagCompressor : IClipCompressor
    {
        public CompressedClip? Compress(AudioClip clip) => new([.. Encoding.ASCII.GetBytes("M4A!"), .. clip.Pcm.Span[..16]], ".m4a");

        public AudioClip? Decompress(byte[] bytes, string name) => null;
    }
}
