using System.Text;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;

// Says a sentence through Edge Neural and writes it out as a WAV, optionally over the comms link
// RadioVoice applies to anybody who is not aboard the ship.
//
//   dotnet run --project spike/RadioAudition -- "text" [out.wav] [--radio] [--voice en-GB-RyanNeural]
//
// The tests assert the filter's shape, its determinism and its loudness. None of them can say
// whether it sounds like a radio, which is why this exists.
var text = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal))
           ?? "Directive 47 is ready.";

var output = args
    .Where(argument => argument.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
    .LastOrDefault() ?? "audition.wav";

var radio = args.Contains("--radio", StringComparer.Ordinal);

var voiceFlag = Array.IndexOf(args, "--voice");
var voice = voiceFlag >= 0 && voiceFlag + 1 < args.Length ? args[voiceFlag + 1] : "en-US-AndrewMultilingualNeural";

var provider = new EdgeNeuralTtsProvider(NullLogger<EdgeNeuralTtsProvider>.Instance);
var clip = await provider.SynthesizeAsync(text, new VoiceSelection(voice));

if (radio)
{
    clip = RadioVoice.Apply(clip);
}

await File.WriteAllBytesAsync(output, Wav(clip));

Console.WriteLine($"{clip.Name} -> {output} ({clip.Duration.TotalSeconds:0.0}s, {(radio ? "radio" : "in the room")})");

// Out loud, on whichever endpoint holds the default — and it says which one that was, because on
// this machine the default is frequently a virtual endpoint nobody is listening to
// (see the silent-default-microphone note: it has faked "d47 can't hear me" twice).
if (args.Contains("--play", StringComparer.Ordinal) || args.Contains("--everywhere", StringComparer.Ordinal))
{
    using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();

    // Every active endpoint, or just the default. --everywhere is for reaching somebody who is
    // wearing a headset: which endpoint they can actually hear is not knowable from here, and a
    // spoken message nobody heard is the same as no message.
    var devices = args.Contains("--everywhere", StringComparer.Ordinal)
        ? enumerator
            .EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active)
            .ToList()
        : [enumerator.GetDefaultAudioEndpoint(
            NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia)];

    foreach (var device in devices)
    {
        Console.WriteLine($"playing on {device.FriendlyName}");

        try
        {
            using var reader = new NAudio.Wave.WaveFileReader(output);
            using var player = new NAudio.Wave.WasapiOut(
                device, NAudio.CoreAudioApi.AudioClientShareMode.Shared, useEventSync: true, latency: 100);

            player.Init(reader);
            player.Play();

            while (player.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  could not: {ex.Message}");
        }
        finally
        {
            device.Dispose();
        }
    }
}

// A 44-byte RIFF header in front of the PCM the arbiter would have played. Deliberately the
// smallest possible writer: this is throwaway, and the app never writes a WAV.
static byte[] Wav(AudioClip clip)
{
    var stream = new MemoryStream();
    var writer = new BinaryWriter(stream, Encoding.ASCII);
    var bytes = clip.Pcm.Length;
    var format = clip.Format;

    writer.Write("RIFF".ToCharArray());
    writer.Write(36 + bytes);
    writer.Write("WAVE".ToCharArray());
    writer.Write("fmt ".ToCharArray());
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)format.Channels);
    writer.Write(format.SampleRate);
    writer.Write(format.SampleRate * format.BytesPerFrame);
    writer.Write((short)format.BytesPerFrame);
    writer.Write((short)16);
    writer.Write("data".ToCharArray());
    writer.Write(bytes);
    writer.Write(clip.Pcm.ToArray());
    writer.Flush();

    return stream.ToArray();
}
