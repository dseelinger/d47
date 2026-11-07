using System.Text;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A run's recorded clip is read back to play: compressed or not, and decrypted when it is in the Commander's own voice.</summary>
public sealed class ARecordedLinesClipCanBeReadBackTests
{
    private const string Commander = "F1";

    private const string Folder = @"C:\d47-test\data\story-runs";

    private static readonly DateTimeOffset Picked = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Story Run = new()
    {
        Id = "the-test-story",
        Title = "The Test Story",
        PublicLayer = "A test.",
        PickedAt = Picked,
        State = StoryState.Running,
        Chapters = ["chapter-one"],
    };

    private static readonly byte[] Pcm = [.. Enumerable.Range(0, 960).Select(index => (byte)index)];

    private readonly MemoryFileSystem _files = new();

    private readonly StoryStore _stories = InMemoryStories.Create();

    private StoryRunLine Recorded(IClipCompressor? compressor, string? voiceId, ISecretProtector? protector = null)
    {
        _stories.Save(Commander, Run);
        var record = new StoryRunRecord(_files, Folder, _stories, () => Commander, compressor, protector ?? new ReversibleProtector(), NullLogger.Instance);
        var messages = new MessageStore(Path.Combine(Folder, "messages.json"), _files, NullLogger<MessageStore>.Instance) { Record = record };

        messages.Post("narrator", "Arrival", "A line.", Picked.AddDays(1), "chapter-one",
            spoken: new SpokenClip([new AudioClip("line", Pcm, AudioFormat.Standard)], "kokoro", voiceId));

        return Assert.Single(record.Read(Commander, Run));
    }

    private StoryRunRecord Record(IClipCompressor? compressor, ISecretProtector? protector = null) =>
        new(_files, Folder, _stories, () => Commander, compressor, protector ?? new ReversibleProtector(), NullLogger.Instance);

    [Fact]
    public void ACompressedClipIsDecompressed()
    {
        var line = Recorded(new TagCompressor(), "voice-a");

        var clip = Record(new TagCompressor()).Clip(Commander, Run, line);

        Assert.Equal(Pcm, clip!.Pcm.ToArray());
    }

    [Fact]
    public void ACompressedClipInTheCommandersOwnVoiceIsDecryptedFirst()
    {
        var line = Recorded(new TagCompressor(), OwnVoice.VoiceId);

        Assert.EndsWith(MessageClips.ProtectedExtension, line.Clip);
        Assert.Equal(Pcm, Record(new TagCompressor()).Clip(Commander, Run, line)!.Pcm.ToArray());
    }

    [Fact]
    public void AClipKeptAsAWavIsReadAsOne()
    {
        var line = Recorded(compressor: null, "voice-a");

        Assert.EndsWith(MessageClips.PlainExtension, line.Clip);
        Assert.Equal(Pcm, Record(compressor: null).Clip(Commander, Run, line)!.Pcm.ToArray());
    }

    [Fact]
    public void AnOwnVoiceClipThatDoesNotDecryptHereIsNotPlayed()
    {
        var line = Recorded(new TagCompressor(), OwnVoice.VoiceId);

        Assert.Null(Record(new TagCompressor(), new NeverUnprotects()).Clip(Commander, Run, line));
    }

    [Fact]
    public void ALineWithNoClipHasNothingToPlay()
    {
        var line = Recorded(new TagCompressor(), "voice-a") with { Clip = null };

        Assert.Null(Record(new TagCompressor()).Clip(Commander, Run, line));
    }

    private sealed class TagCompressor : IClipCompressor
    {
        private static readonly byte[] Tag = Encoding.ASCII.GetBytes("M4A!");

        public CompressedClip? Compress(AudioClip clip) => new([.. Tag, .. clip.Pcm.Span], ".m4a");

        public AudioClip? Decompress(byte[] bytes, string name) =>
            bytes.AsSpan().StartsWith(Tag) ? new AudioClip(name, bytes[Tag.Length..], AudioFormat.Standard) : null;
    }
}
