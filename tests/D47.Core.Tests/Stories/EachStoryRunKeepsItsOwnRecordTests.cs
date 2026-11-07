using System.Text;
using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>Every line a stock story posts is kept in its run's own record, with its clip, outside the 200 messages.</summary>
public sealed class EachStoryRunKeepsItsOwnRecordTests
{
    private const string Commander = "F1";

    private const string StoryId = "the-test-story";

    private const string Folder = @"C:\d47-test\data\story-runs";

    private static readonly DateTimeOffset Picked = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private readonly StoryStore _stories = InMemoryStories.Create();

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

    private StoryRunRecord Record(IClipCompressor? compressor = null) => new(
        _files, Folder, _stories, () => Commander, compressor ?? new TaggingCompressor(), new ReversibleProtector(), NullLogger.Instance);

    private MessageStore Messages(StoryRunRecord record) =>
        new(Path.Combine(Folder, "messages.json"), _files, NullLogger<MessageStore>.Instance) { Record = record };

    private byte[]? ClipBytes(Story run, string file) =>
        _files.ReadBytes(Path.Combine(Folder, StoryRunRecord.FolderName(Commander, run.Id, run.PickedAt), file));

    [Fact]
    public void AChapterLineGoesToTheRunThatListsTheChapter()
    {
        var first = Run(Picked, StoryState.Abandoned, "chapter-one");
        var second = Run(Picked.AddDays(3), StoryState.Running, "chapter-two");
        _stories.Save(Commander, first);
        _stories.Save(Commander, second);
        var record = Record();

        Messages(record).Post("narrator", "Chapter one", "An old beat.", Picked.AddDays(5), "chapter-one");

        Assert.Equal("An old beat.", Assert.Single(record.Read(Commander, first)).Body);
        Assert.Empty(record.Read(Commander, second));
    }

    [Fact]
    public void AStoryLineGoesToTheCurrentRunOfThatStory()
    {
        var first = Run(Picked, StoryState.Abandoned, "chapter-one");
        var second = Run(Picked.AddDays(3), StoryState.Running, "chapter-two");
        _stories.Save(Commander, first);
        _stories.Save(Commander, second);
        var record = Record();

        Messages(record).Post("narrator", "The Test Story", "A line.", Picked.AddDays(5), StoryLines.Key(StoryId));

        Assert.Single(record.Read(Commander, second));
        Assert.Empty(record.Read(Commander, first));
    }

    [Fact]
    public void AStoryLineWithNoCurrentRunGoesToTheNewest()
    {
        var first = Run(Picked, StoryState.Finished, "chapter-one");
        var second = Run(Picked.AddDays(3), StoryState.Ended, "chapter-two");
        _stories.Save(Commander, first);
        _stories.Save(Commander, second);
        var record = Record();

        Messages(record).Post("ship-ai", "The Test Story", "The last word.", Picked.AddDays(5), StoryEnding.Key(StoryId));

        Assert.Single(record.Read(Commander, second));
        Assert.Empty(record.Read(Commander, first));
    }

    [Fact]
    public void ShortStoryLinesAndUntiedLinesAreNotRecorded()
    {
        _stories.Save(Commander, Run(Picked, StoryState.Running, "chapter-one"));
        var messages = Messages(Record());

        messages.Post("narrator", "A short story", "A beat.", Picked.AddDays(1), "short-story-key", spoken: Spoken());
        messages.Post("ship-ai", "Hello", "Nothing to do with a story.", Picked.AddDays(1), spoken: Spoken());
        messages.Post("narrator", "Another", "Someone else's story.", Picked.AddDays(1), StoryLines.Key("a-story-never-picked"));

        Assert.Equal(["messages.json"], _files.Enumerate(Folder, "*", recursive: true).Select(Path.GetFileName));
    }

    [Fact]
    public void ALineKeepsWhoSentItAndItsClipInPostingOrder()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();
        var messages = Messages(record);

        var first = messages.Post("Ada Reyes", "Arrival", "First.", Picked.AddDays(1), "chapter-one", picture: "the-test-story.ada", spoken: Spoken(), cast: "the-test-story.ada");
        messages.Post("narrator", "Arrival", "Second.", Picked.AddDays(1).AddSeconds(5), StoryLines.Key(StoryId));

        var lines = record.Read(Commander, run);

        Assert.Equal(["First.", "Second."], lines.Select(line => line.Body));
        Assert.Equal(
            new StoryRunLine
            {
                Sent = first.Sent,
                From = "Ada Reyes",
                Cast = "the-test-story.ada",
                Picture = "the-test-story.ada",
                Subject = "Arrival",
                Body = "First.",
                Key = first.Key,
                AdventureKey = "chapter-one",
                Clip = first.Key + TaggingCompressor.Extension,
                Voice = new MessageVoice("kokoro", "voice-a"),
            },
            lines[0]);
        Assert.Null(lines[1].Clip);
    }

    [Fact]
    public void AClipAttachedAfterThePostReachesTheRecordWithNoMessageClipFolder()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();
        var messages = Messages(record);

        var posted = messages.Post("narrator", "Arrival", "Said later.", Picked.AddDays(1), "chapter-one");
        messages.Attach(posted.Key, Spoken());

        var line = Assert.Single(record.Read(Commander, run));
        Assert.Equal(posted.Key + TaggingCompressor.Extension, line.Clip);
        Assert.Equal(new MessageVoice("kokoro", "voice-a"), line.Voice);
        Assert.Equal(TaggingCompressor.Tag, Encoding.ASCII.GetString(ClipBytes(run, line.Clip!)!, 0, TaggingCompressor.Tag.Length));
    }

    [Fact]
    public void AClipThatDoesNotCompressIsKeptAsAWav()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record(new FailingCompressor());

        var posted = Messages(record).Post("narrator", "Arrival", "Uncompressed.", Picked.AddDays(1), "chapter-one", spoken: Spoken());

        var line = Assert.Single(record.Read(Commander, run));
        Assert.Equal(posted.Key + MessageClips.PlainExtension, line.Clip);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(ClipBytes(run, line.Clip!)!, 0, 4));
    }

    [Fact]
    public void AClipInTheCommandersOwnVoiceIsProtectedAfterCompressing()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();

        var posted = Messages(record).Post("ship-ai", "Arrival", "In my voice.", Picked.AddDays(1), "chapter-one", spoken: Spoken(OwnVoice.VoiceId));

        var line = Assert.Single(record.Read(Commander, run));
        Assert.Equal(posted.Key + TaggingCompressor.Extension + MessageClips.ProtectedExtension, line.Clip);
        Assert.True(new ReversibleProtector().TryUnprotect(ClipBytes(run, line.Clip!)!, out var plain));
        Assert.Equal(TaggingCompressor.Tag, Encoding.ASCII.GetString(plain, 0, TaggingCompressor.Tag.Length));
    }

    [Fact]
    public void TheMessageCapAndTheOrphanSweepDeleteNothingFromTheRecord()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();
        var messages = Messages(record);

        for (var i = 0; i <= MessageStore.Capacity; i++)
        {
            messages.Post("narrator", "Arrival", $"Line {i}.", Picked.AddMinutes(i), "chapter-one", spoken: Spoken());
        }

        messages.RemoveOrphans(_ => false);

        var lines = record.Read(Commander, run);
        Assert.Empty(messages.All);
        Assert.Equal(MessageStore.Capacity + 1, lines.Count);
        Assert.All(lines, line => Assert.NotNull(ClipBytes(run, line.Clip!)));
    }

    [Fact]
    public void ForgettingTheOwnVoiceDeletesItsClipsAndKeepsTheText()
    {
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();
        var messages = Messages(record);

        var own = messages.Post("ship-ai", "Arrival", "In my voice.", Picked.AddDays(1), "chapter-one", spoken: Spoken(OwnVoice.VoiceId));
        var other = messages.Post("ship-ai", "Arrival", "In another.", Picked.AddDays(1), "chapter-one", spoken: Spoken());
        var ownClip = record.Read(Commander, run)[0].Clip!;

        messages.ForgetOwnVoice();

        var lines = record.Read(Commander, run);
        Assert.Equal(new StoryRunLine { Sent = own.Sent, From = "ship-ai", Subject = "Arrival", Body = "In my voice.", Key = own.Key, AdventureKey = "chapter-one" }, lines[0]);
        Assert.Null(ClipBytes(run, ownClip));
        Assert.Equal(other.Key + TaggingCompressor.Extension, lines[1].Clip);
    }

    [Fact]
    public void ForgettingACustomVoiceDeletesOnlyItsClips()
    {
        const string gone = "my-0000abcd";
        const string kept = "my-1234abcd";
        var run = Run(Picked, StoryState.Running, "chapter-one");
        _stories.Save(Commander, run);
        var record = Record();
        var messages = Messages(record);

        messages.Post("Ada Reyes", "Arrival", "Gone voice.", Picked.AddDays(1), "chapter-one", spoken: Spoken(gone));
        messages.Post("Ada Reyes", "Arrival", "Kept voice.", Picked.AddDays(1), "chapter-one", spoken: Spoken(kept));

        messages.ForgetCustomVoice(gone);

        var lines = record.Read(Commander, run);
        Assert.Equal(("Gone voice.", null, null), (lines[0].Body, lines[0].Clip, lines[0].Voice));
        Assert.Equal(new MessageVoice("kokoro", kept), lines[1].Voice);
        Assert.NotNull(ClipBytes(run, lines[1].Clip!));
    }

    [Fact]
    public void AnEraseDeletesTheRecordFoldersOfTheRunsItRemoves()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider([]));
        var erased = Run(Picked, StoryState.Finished, "chapter-one");
        var later = Run(Picked.AddDays(10), StoryState.Running, "chapter-two");
        var stories = fixtures.Stories;
        stories.Save(Commander, erased);
        stories.Save(Commander, later);
        var record = new StoryRunRecord(fixtures.Files, Folder, stories, () => Commander, new TaggingCompressor(), new ReversibleProtector(), NullLogger.Instance);
        fixtures.Director.Records = record;
        var messages = new MessageStore(Path.Combine(Folder, "messages.json"), fixtures.Files, NullLogger<MessageStore>.Instance) { Record = record };

        messages.Post("narrator", "Old", "Before the erase.", Picked.AddDays(1), "chapter-one", spoken: Spoken());
        messages.Post("narrator", "New", "After the erase.", Picked.AddDays(11), "chapter-two", spoken: Spoken());

        fixtures.Director.ForgetErased(Commander, Picked.AddDays(5));

        var erasedFolder = Path.Combine(Folder, StoryRunRecord.FolderName(Commander, StoryId, erased.PickedAt));
        Assert.Null(fixtures.Files.FolderWritten(erasedFolder));
        Assert.Single(record.Read(Commander, later));
    }

    [Fact]
    public void RunsPickedInTheSameSecondHaveTheirOwnFolders() =>
        Assert.NotEqual(
            StoryRunRecord.FolderName(Commander, StoryId, Picked),
            StoryRunRecord.FolderName(Commander, StoryId, Picked.AddMilliseconds(1)));

    private sealed class TaggingCompressor : IClipCompressor
    {
        public const string Tag = "M4A!";

        public const string Extension = ".m4a";

        public CompressedClip? Compress(AudioClip clip) => new([.. Encoding.ASCII.GetBytes(Tag), .. clip.Pcm.Span[..16]], Extension);

        public AudioClip? Decompress(byte[] bytes, string name) => null;
    }

    private sealed class FailingCompressor : IClipCompressor
    {
        public CompressedClip? Compress(AudioClip clip) => null;

        public AudioClip? Decompress(byte[] bytes, string name) => null;
    }
}
