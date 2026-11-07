using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ambient music over a temp folder of wav tracks, an arbiter and a recording sink.</summary>
[Trait("Category", "Integration")]
internal sealed class MusicBench : IDisposable
{
    private readonly string _root;

    public MusicBench(string folder, params (string Situation, string Track)[] tracks)
        : this(folder, lastFolder: null, tracks)
    {
    }

    public MusicBench(string folder, string? lastFolder, params (string Situation, string Track)[] tracks)
    {
        _root = Path.Combine(Path.GetTempPath(), folder, Guid.NewGuid().ToString("n"));
        Sink = new RecordingAudioSink();
        Audio = new AudioArbiter(Sink, NullLogger<AudioArbiter>.Instance).Start();

        var library = Library(tracks);

        Music = new AmbientMusic(Audio, () => library, new Random(7), lastFolder: lastFolder);
        Audio.MusicFinished += Music.TrackFinished;
    }

    public RecordingAudioSink Sink { get; }

    public AudioArbiter Audio { get; }

    public AmbientMusic Music { get; }

    public CueLibrary Library(params (string Situation, string Track)[] tracks)
    {
        foreach (var (situation, track) in tracks)
        {
            var path = Path.Combine(_root, FolderAudioSource.MusicFolder, situation, $"{track}.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, WavWriter.ToBytes(new byte[480], AudioFormat.Standard));
        }

        return CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(_root, new DiskFileSystem(), NullLogger<FolderAudioSource>.Instance));
    }

    public void Dispose()
    {
        Audio.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }
}
