using D47.Core.Audio;
using D47.Core.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Messages;

[Trait("Category", "Integration")]
public class ASpokenMessageKeepsItsClipTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-message-clips", Guid.NewGuid().ToString("N"));

    public ASpokenMessageKeepsItsClipTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string ClipFolder => Path.Combine(_folder, "messages");

    private MessageStore Open() => new(
        Path.Combine(_folder, "messages.json"),
        NullLogger<MessageStore>.Instance,
        new MessageClips(ClipFolder, new ReversibleProtector()));

    private static SpokenClip Spoken(byte seed, string? voice = "af_heart") =>
        new([Part(seed, 480), Part((byte)(seed + 1), 960)], "kokoro", voice);

    private static AudioClip Part(byte seed, int bytes) =>
        new("line", Enumerable.Range(0, bytes).Select(i => (byte)(seed + i)).ToArray(), AudioFormat.Standard);

    private static byte[] Played(SpokenClip spoken) => spoken.Joined("line").Pcm.ToArray();

    private string[] FilesOnDisk() =>
        Directory.Exists(ClipFolder) ? [.. Directory.GetFiles(ClipFolder).Select(Path.GetFileName).OfType<string>()] : [];

    [Fact]
    public void ASpokenBeatPlaysTheSameAudioAfterARestart()
    {
        var spoken = Spoken(3);
        var posted = Open().Post("covas", "Beat", "We have arrived.", Noon, "story.lines.x", spoken: spoken);

        var reopened = Open();
        var message = Assert.Single(reopened.All);

        Assert.Equal(new MessageVoice("kokoro", "af_heart"), message.Voice);
        Assert.Equal(posted.Clip, message.Clip);
        Assert.Equal(Played(spoken), reopened.ClipOf(message)!.Pcm.ToArray());
        Assert.Equal("We have arrived.", reopened.ClipOf(message)!.Name);
    }

    [Fact]
    public void AClipAttachedAfterTheLineIsSaidIsKept()
    {
        var store = Open();
        var posted = store.Post("narrator", "Scan", "The beacon sings.", Noon);
        var spoken = Spoken(9);

        Assert.True(store.Attach(posted.Key, spoken));

        var message = Assert.Single(Open().All);
        Assert.Equal(Played(spoken), Open().ClipOf(message)!.Pcm.ToArray());
    }

    [Fact]
    public void AMessageThatWasNotSpokenHasNoClip()
    {
        var store = Open();
        var message = store.Post("narrator", "Narration", "Quiet out here.", Noon);

        Assert.Null(message.Clip);
        Assert.Null(message.Voice);
        Assert.Null(store.ClipOf(message));
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public void PassingTheCapDeletesTheEvictedMessagesClip()
    {
        var store = Open();
        var first = store.Post("covas", "First", "one", Noon, spoken: Spoken(1));

        for (var i = 0; i < MessageStore.Capacity; i++)
        {
            store.Post("covas", $"Later {i}", "more", Noon.AddMinutes(i + 1), spoken: i % 50 == 0 ? Spoken(2) : null);
        }

        Assert.DoesNotContain(store.All, message => message.Key == first.Key);
        Assert.DoesNotContain(first.Clip, FilesOnDisk());
        Assert.Equal(
            store.All.Select(message => message.Clip).OfType<string>().Order(),
            FilesOnDisk().Order());
    }

    [Fact]
    public void AClipForAMessageAlreadyGoneIsNotKept()
    {
        var store = Open();

        Assert.False(store.Attach("no-such-message", Spoken(4)));
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public void AStrayFileIsSweptWhenTheStoreIsRead()
    {
        Directory.CreateDirectory(ClipFolder);
        File.WriteAllBytes(Path.Combine(ClipFolder, "left-behind.wav"), [1, 2, 3]);

        var store = Open();
        var kept = store.Post("covas", "Beat", "Kept.", Noon, spoken: Spoken(5));

        Assert.Equal([kept.Clip!], FilesOnDisk());
    }

    [Fact]
    public void AnOrphanedMessageTakesItsClipWithIt()
    {
        var store = Open();
        store.Post("covas", "Beat", "Gone.", Noon, "abandoned-story", spoken: Spoken(6));

        Assert.Equal(1, store.RemoveOrphans(_ => false));
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public void AnOwnVoiceClipOnDiskIsNotAReadableWavAndStillPlays()
    {
        var spoken = Spoken(7, OwnVoice.VoiceId);
        var store = Open();
        var message = store.Post("narrator", "Mayday", "This is my own voice.", Noon, spoken: spoken);

        var bytes = File.ReadAllBytes(Path.Combine(ClipFolder, message.Clip!));

        Assert.False(bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8));
        Assert.Throws<WavFormatException>(() => WavReader.Read(new MemoryStream(bytes), "own"));
        Assert.Equal(Played(spoken), Open().ClipOf(message)!.Pcm.ToArray());
    }

    [Fact]
    public void DeletingTheRecordingDeletesOwnVoiceClipsAndKeepsTheirMessages()
    {
        var store = Open();
        var own = store.Post("narrator", "Mayday", "In my voice.", Noon, spoken: Spoken(8, OwnVoice.VoiceId));
        var other = store.Post("covas", "Beat", "In the ship's.", Noon.AddMinutes(1), spoken: Spoken(9));

        Assert.Equal(1, store.ForgetOwnVoice());

        var kept = Open().All.Single(message => message.Key == own.Key);

        Assert.Equal("In my voice.", kept.Body);
        Assert.Null(kept.Clip);
        Assert.Null(kept.Voice);
        Assert.Equal([other.Clip!], FilesOnDisk());
    }

    [Fact]
    public void DeletingACustomVoiceDeletesItsClipsAndKeepsTheirMessages()
    {
        var store = Open();
        var gone = store.Post("narrator", "Mayday", "In one custom voice.", Noon, spoken: Spoken(10, "my-0badf00d"));
        var kept = store.Post("narrator", "Mayday", "In another.", Noon.AddMinutes(1), spoken: Spoken(11, "my-1badf00d"));
        var own = store.Post("narrator", "Mayday", "In mine.", Noon.AddMinutes(2), spoken: Spoken(12, OwnVoice.VoiceId));

        Assert.Equal(1, store.ForgetCustomVoice("my-0badf00d"));

        var reopened = Open().All;

        Assert.Null(reopened.Single(message => message.Key == gone.Key).Clip);
        Assert.Equal("In one custom voice.", reopened.Single(message => message.Key == gone.Key).Body);
        Assert.NotNull(reopened.Single(message => message.Key == kept.Key).Clip);
        Assert.NotNull(reopened.Single(message => message.Key == own.Key).Clip);
        Assert.Equal(1, store.ForgetOwnVoice());
        Assert.NotNull(Open().All.Single(message => message.Key == kept.Key).Clip);
    }
}
