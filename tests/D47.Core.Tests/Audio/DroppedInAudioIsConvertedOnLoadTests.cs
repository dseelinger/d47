using System.Buffers.Binary;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A drop-in in any rate, channel count or decodable format is converted, not refused.</summary>
public class DroppedInAudioIsConvertedOnLoadTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-audio-convert-tests",
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
    public void AStereoFortyFourKilohertzWavLoadsWithoutASkipLine()
    {
        Write("beds/stereo-bed.wav", WavWriter.ToBytes(Pcm(4410 * 2, (_, _) => 1000), new AudioFormat(44_100, 2)));

        var library = Load();

        Assert.Empty(library.Skipped);

        var bed = library.Bed("stereo-bed");
        Assert.Equal("stereo-bed", bed.Name);
        Assert.Equal(AudioFormat.Standard, bed.Format);
        Assert.InRange(bed.Duration.TotalMilliseconds, 99, 101);
    }

    [Fact]
    public void AStereoMusicTrackStreamsInTheStandardFormat()
    {
        Write("music/general/wide.wav", WavWriter.ToBytes(Pcm(22_050 * 2, (_, _) => 500), new AudioFormat(22_050, 2)));

        using var stream = Assert.Single(Load().Music(Situations.General)).Open();
        var pcm = ReadAll(stream, chunk: 6);

        Assert.InRange(pcm.Length / 2, 47_990, 48_010);
        Assert.All(Samples(pcm), sample => Assert.Equal(500, sample));
    }

    [Fact]
    public void ChannelsAreAveragedIntoOne()
    {
        var stereo = Pcm(8, (frame, index) => index % 2 == 0 ? 1000 : -200);
        var clip = PcmConverter.ToStandard(new AudioClip("pair", stereo, new AudioFormat(48_000, 2)));

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.All(Samples(clip.Pcm.ToArray()), sample => Assert.Equal(400, sample));
    }

    [Fact]
    public void ResamplingInterpolatesBetweenSourceSamples()
    {
        // 24 kHz to 48 kHz puts every other output sample halfway between two inputs.
        var rising = Pcm(4, (frame, _) => frame * 100, channels: 1);
        var clip = PcmConverter.ToStandard(new AudioClip("ramp", rising, new AudioFormat(24_000, 1)));

        Assert.Equal([0, 50, 100, 150, 200, 250, 300, 300], Samples(clip.Pcm.ToArray()));
    }

    [Fact]
    public void AStreamReadInSmallPiecesMatchesTheWholeConversion()
    {
        var bytes = WavWriter.ToBytes(Pcm(3000, (frame, _) => frame * 7, channels: 1), new AudioFormat(44_100, 1));

        var whole = PcmConverter.ToStandard(WavReader.Read(new MemoryStream(bytes), "whole")).Pcm.ToArray();
        using var stream = WavReader.OpenStandard(new MemoryStream(bytes), "pieces");

        Assert.Equal(whole, ReadAll(stream, chunk: 10));
    }

    [Fact]
    public void AStandardWavIsPassedThroughUntouched()
    {
        var pcm = Pcm(5, (frame, _) => frame - 2, channels: 1);
        var clip = new AudioClip("same", pcm, AudioFormat.Standard);

        Assert.Same(clip, PcmConverter.ToStandard(clip));
    }

    /// <summary>Headless, there is no decoder, and only the reader Core carries is used.</summary>
    [Fact]
    public void WithoutADecoderOnlyWavIsPickedUp()
    {
        Write("beds/song.mp3", [1, 2, 3]);

        Assert.DoesNotContain(Source(decoder: null).Names, name => name.Contains("song", StringComparison.Ordinal));
    }

    [Fact]
    public void AFormatTheDecoderListsIsReadThroughIt()
    {
        Write("beds/song.mp3", [1, 2, 3]);
        Write("music/docked/tune.flac", [1, 2, 3]);

        var decoder = new FakeDecoder();
        var library = LoadWith(decoder);

        Assert.Empty(library.Skipped);
        Assert.Equal(FakeDecoder.Pcm, library.Bed("song").Pcm.ToArray());

        using var stream = Assert.Single(library.Music(Situations.Docked)).Open();
        Assert.Equal(FakeDecoder.Pcm, ReadAll(stream, chunk: 64));
    }

    /// <summary>A file Windows cannot decode is a line on the settings row, and d47 carries on.</summary>
    [Fact]
    public void AFileTheDecoderRefusesIsSkippedWithItsReason()
    {
        Write("beds/broken.mp3", [1, 2, 3]);
        Write("music/general/broken.m4a", [1, 2, 3]);

        var library = LoadWith(new FakeDecoder { Refuse = true });

        Assert.DoesNotContain("broken", library.BedNames);
        Assert.Empty(library.Music(Situations.General));
        Assert.Equal(2, library.Skipped.Count);
        Assert.All(library.Skipped, reason => Assert.Contains(FakeDecoder.Reason, reason, StringComparison.Ordinal));
    }

    /// <summary>A Windows without Media Foundation can still read the WAVs Core can.</summary>
    [Fact]
    public void AWavTheDecoderRefusesFallsBackToCoresReader()
    {
        Write("beds/plain.wav", WavWriter.ToBytes(Pcm(480, (_, _) => 7), AudioFormat.Standard));

        var library = LoadWith(new FakeDecoder { Refuse = true });

        Assert.Empty(library.Skipped);
        Assert.Equal(TimeSpan.FromMilliseconds(10), library.Bed("plain").Duration);
    }

    [Fact]
    public void AWavThatIsNotAudioIsSkippedAndNamed()
    {
        Write("beds/noise.wav", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        var library = Load();

        Assert.DoesNotContain("noise", library.BedNames);
        Assert.Contains("noise", Assert.Single(library.Skipped), StringComparison.Ordinal);
    }

    private sealed class FakeDecoder : IAudioDecoder
    {
        public const string Reason = "Windows has no decoder for this file.";

        public static readonly byte[] Pcm = [1, 0, 2, 0, 3, 0];

        public bool Refuse { get; init; }

        public IReadOnlySet<string> Extensions { get; } =
            new HashSet<string> { ".mp3", ".m4a", ".flac", ".wav" };

        public AudioClip Decode(string path, string name) =>
            Refuse ? throw new AudioDecodeException(Reason) : new AudioClip(name, Pcm, AudioFormat.Standard);

        public IPcmStream Open(string path) =>
            Refuse
                ? throw new AudioDecodeException(Reason)
                : WavReader.Open(new MemoryStream(WavWriter.ToBytes(Pcm, AudioFormat.Standard)), "fake");
    }

    private FolderAudioSource Source(IAudioDecoder? decoder) =>
        new(_root, NullLogger<FolderAudioSource>.Instance, decoder);

    private CueLibrary Load() => LoadWith(decoder: null);

    private CueLibrary LoadWith(IAudioDecoder? decoder) =>
        CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly), Source(decoder));

    private void Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>Interleaved 16-bit samples; <paramref name="sample"/> takes the frame and the sample index.</summary>
    private static byte[] Pcm(int samples, Func<int, int, int> sample, int channels = 2)
    {
        var bytes = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), (short)sample(index / channels, index));
        }

        return bytes;
    }

    private static short[] Samples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(index * 2));
        }

        return samples;
    }

    private static byte[] ReadAll(IPcmStream stream, int chunk)
    {
        using var output = new MemoryStream();
        var buffer = new byte[chunk];
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
