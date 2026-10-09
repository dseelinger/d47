using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Every file under <c>data\audio</c> lands in a folder count, an Ignored line or a Skipped line (#499).</summary>
[Trait("Category", "Integration")]
public class TheRowAccountsForEveryDroppedFileTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-audio-accounting-tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    [Fact]
    public void EveryFileIsCountedIgnoredOrSkipped()
    {
        for (var index = 0; index < 5; index++)
        {
            WriteWav($"cues/listening/take-{index}.wav");
        }

        WriteWav("alerts/under-fire/siren.wav");
        WriteWav("beds/hum.wav");

        for (var index = 0; index < 12; index++)
        {
            WriteWav($"music/combat-dogfight/track-{index}.wav");
        }

        WriteWav("alerts/klaxon.wav");
        File.WriteAllText(Path.Combine(_root, "music", "notes.txt"), "not audio");
        WriteWav("playlist/mix.wav");

        var decoder = new RefusingDecoder();
        WriteBytes("beds/broken.mp3", [1, 2, 3]);

        var source = new FolderAudioSource(_root, NullLogger<FolderAudioSource>.Instance, decoder);
        var library = CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly), source);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "cues/listening: 5 files",
                "alerts/under-fire: 1 file",
                "beds: 1 file",
                "music/combat-dogfight: 12 files",
                "Ignored: klaxon.wav is directly in alerts; put it in a folder such as alerts/under-fire.",
                "Ignored: music/notes.txt is not an audio format D47 reads.",
                "Ignored: playlist/mix.wav is in a folder D47 does not read; folders are cues, alerts, beds and music.",
                $"Skipped: broken: {RefusingDecoder.Reason}"),
            library.DescribeDrops());
    }

    private void WriteWav(string relative)
    {
        const int Samples = 240;
        var data = Samples * 2;

        using var file = File.Create(FullPath(relative));
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write((short)1);
        write.Write(48_000);
        write.Write(48_000 * 2);
        write.Write((short)2);
        write.Write((short)16);
        write.Write("data"u8.ToArray());
        write.Write(data);
        write.Write(new byte[data]);
    }

    private void WriteBytes(string relative, byte[] bytes) => File.WriteAllBytes(FullPath(relative), bytes);

    private string FullPath(string relative)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private sealed class RefusingDecoder : IAudioDecoder
    {
        public const string Reason = "Windows could not decode it.";

        public IReadOnlySet<string> Extensions { get; } = new HashSet<string> { ".mp3" };

        public AudioClip Decode(string path, string name) => throw new AudioDecodeException(Reason);

        public IPcmStream Open(string path) => throw new AudioDecodeException(Reason);
    }
}
