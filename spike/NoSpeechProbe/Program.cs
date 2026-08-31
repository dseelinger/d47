// Can d47 tell "the Commander said something" from "the Commander pressed push-to-talk to shut
// d47 up and said nothing" — and where?
//
// The report: pressing push-to-talk to interrupt produced a turn, because a second of room tone
// came back as "Thank you for watching!" — the canonical Whisper hallucination on silence, and
// unbracketed, so SpeechNoise does not see it.
//
// FINDING 1, and it rules out the obvious fix. segment.NoSpeechProbability separates the two
// populations perfectly *until* the name-hint prompt is applied — and d47 always applies it. On
// room tone: 0.96 with no prompt, 0.0001 with 36 hints, which is what real speech reads. The
// prompt is also what turns [BLANK_AUDIO] into words: unprompted every silent clip says
// [BLANK_AUDIO], and prompted the same clip says "A*", out of the hint vocabulary. So the prompt
// causes the hallucination AND destroys the signal that would have caught it, and asking Whisper
// twice — once primed, once not — doubles a cost already measured in seconds.
//
// So the decision has to be made from the audio, before the transcriber is reached. This measures
// the candidates on two populations that have to separate: the Commander's own recorded utterances
// (must survive) and room tone (must be discarded), including the quietest second sliced out of
// each real recording, which is room tone from the real microphone rather than a synthetic guess.
//
// FINDING 2. An energy rule over the clip does not work. At VoiceActivityDetector's own absolute
// floor (-55 dBFS, 3 frames) it discards three of the Commander's real utterances, whose peaks are
// -52.6, -48.5 and -47.4 dBFS, and it passes synthetic room tone at -50. An absolute threshold is
// the wrong instrument, which is exactly what that detector's own doc says. And making push-to-talk
// depend on the *adaptive* detector instead is forbidden by ListenGate.KeyDown: "A Commander who
// wants to be certain d47 is listening should not have to trust a detector to agree." Tried; it
// broke nine existing gate guarantees, which is that rule being enforced by the tests.
//
// FINDING 3, and this is the one that works. A second UNPROMPTED pass on ggml-tiny.en, purely for
// the no-speech figure, separates the two populations cleanly and costs about 350 ms:
//
//     real speech        0.0167 - 0.2592   (8 of the Commander's own utterances)
//     its own room tone  0.9464 - 0.9580   (the quietest second of each of those recordings)
//
// The cost is flat in clip length — 345 ms for one second, 422 ms for 13.8 — because it is the
// 30 s mel padding rather than the audio. Against the 2.9-4.6 s the primed pass already takes,
// that is about a tenth, and it can run concurrently with it so real speech waits no longer.
//
//   dotnet run --project spike/NoSpeechProbe -c Release
//   dotnet run --project spike/NoSpeechProbe -c Release -- --check <path to ggml-tiny.en.bin>
//
// Throwaway. See spike/README.md.

using System.Diagnostics;
using System.Globalization;
using D47.Core.Audio;
using D47.Core.Listening;
using Whisper.net;

var installed = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Programs",
    "d47");

var clipDirectory = Arg("--clips") ?? Path.Combine(installed, "data", "flight");
var maxClips = int.Parse(Arg("--max-clips") ?? "40", CultureInfo.InvariantCulture);

Console.WriteLine($"clips {clipDirectory}");
Console.WriteLine();
Console.WriteLine($"{"clip",-30} {"kind",6} {"secs",5} {"peak dB",8} {"p50 dB",8} {"range",7} {"loud%",7} {"run",6}  verdict");
Console.WriteLine(new string('-', 100));

// FINDING 2 ruled out the energy rule below: at VoiceActivityDetector's own -55 dBFS floor it
// discards three of the Commander's real utterances (peaks -52.6, -48.5, -47.4 dBFS). An absolute
// threshold is the wrong instrument, which is what the detector's own doc says — and making
// push-to-talk depend on the *adaptive* detector is forbidden by ListenGate.KeyDown: "A Commander
// who wants to be certain d47 is listening should not have to trust a detector to agree."
//
// So this measures the remaining candidate: a second, UNPROMPTED pass purely for the no-speech
// figure, on the cheapest model d47 already ships. Prompted decoding is what destroys the signal,
// so the check has to be its own pass — the question is what that pass costs.
var check = Arg("--check");

if (check is not null)
{
    using var factory = WhisperFactory.FromPath(check, new WhisperFactoryOptions { UseGpu = false });

    Console.WriteLine($"silence check: {Path.GetFileName(check)}, unprompted");
    Console.WriteLine();
    Console.WriteLine($"{"clip",-30} {"kind",6} {"secs",5} {"no-speech",10} {"ms",6}  text");
    Console.WriteLine(new string('-', 96));

    foreach (var clip in Recorded())
    {
        using var processor = factory.CreateBuilder().WithLanguage("en").WithProbabilities().Build();

        var watch = Stopwatch.StartNew();
        var noSpeech = 1d;
        var text = string.Empty;

        await foreach (var segment in processor.ProcessAsync(clip.Utterance.Samples))
        {
            noSpeech = Math.Min(noSpeech, segment.NoSpeechProbability);
            text += segment.Text;
        }

        watch.Stop();

        Console.WriteLine(
            $"{clip.Name,-30} {clip.Kind,6} {clip.Utterance.Duration.TotalSeconds,5:0.0}"
            + $" {noSpeech,10:0.0000} {watch.ElapsedMilliseconds,6}  "
            + $"{(text.Length > 30 ? text[..30] + "…" : text).Trim()}");
    }

    return;
}

foreach (var clip in Recorded())
{
    Report(clip);
}

Console.WriteLine();

foreach (var clip in Synthetic())
{
    Report(clip);
}

void Report(Clip clip)
{
    var frames = Frames(clip.Utterance.Samples);

    if (frames.Count == 0)
    {
        return;
    }

    var sorted = frames.Order().ToList();
    var peak = sorted[^1];
    var p50 = sorted[sorted.Count / 2];

    // Only a candidate definition: frames within 10 dB of the loudest. Speech is bursty, so a real
    // utterance has a minority of them; flat room tone has nearly all its frames there, which is
    // why the share is reported rather than a count.
    var loud = frames.Count(db => db > peak - 10) * 100.0 / frames.Count;

    // THE CANDIDATE RULE: the longest run of consecutive frames above VoiceActivityDetector's own
    // absolute floor. Both numbers are already in the codebase and already reasoned about —
    // Minimum = -55 dBFS and Onset = 3 frames — so this is the detector's existing judgement
    // asked of a finished clip rather than a second threshold invented here.
    var run = 0;
    var longest = 0;

    foreach (var db in frames)
    {
        run = db > -55 ? run + 1 : 0;
        longest = Math.Max(longest, run);
    }

    Console.WriteLine(
        $"{clip.Name,-30} {clip.Kind,6} {clip.Utterance.Duration.TotalSeconds,5:0.0}"
        + $" {peak,8:0.0} {p50,8:0.0} {peak - p50,7:0.0} {loud,7:0.0} {longest,6}"
        + $"  {(longest >= 3 ? "SPEECH" : "discard")}");
}

// 20 ms frames, the same window VoiceActivityDetector uses, in dBFS.
static List<double> Frames(float[] samples)
{
    const int Size = 320;
    var frames = new List<double>();

    for (var at = 0; at + Size <= samples.Length; at += Size)
    {
        var sum = 0d;

        for (var i = at; i < at + Size; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        var rms = Math.Sqrt(sum / Size);
        frames.Add(rms <= 1e-10 ? -100 : Math.Max(-100, 20 * Math.Log10(rms)));
    }

    return frames;
}

IEnumerable<Clip> Synthetic()
{
    foreach (var db in new[] { -70, -60, -50, -40, -35 })
    {
        yield return new Clip($"synthetic tone {db} dBFS", "tone", new Utterance(Noise(1.0, db), 16000));
    }

    static float[] Noise(double seconds, int db)
    {
        var random = new Random(47);
        var amplitude = Math.Pow(10, db / 20.0);
        var samples = new float[(int)(seconds * 16000)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)((random.NextDouble() * 2 - 1) * amplitude);
        }

        return samples;
    }
}

IEnumerable<Clip> Recorded()
{
    if (!Directory.Exists(clipDirectory))
    {
        yield break;
    }

    var paths = Directory
        .EnumerateFiles(clipDirectory, "*-heard.wav")
        .OrderByDescending(path => path, StringComparer.Ordinal)
        .Take(maxClips)
        .Reverse();

    foreach (var path in paths)
    {
        var clip = WavReader.Read(path);
        var pcm = clip.Pcm.Span;
        var samples = new float[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        var name = Path.GetFileNameWithoutExtension(path)
            .Replace("-heard", string.Empty, StringComparison.Ordinal);

        yield return new Clip(name, "said", new Utterance(samples, clip.Format.SampleRate));

        // The quietest second of the same recording: the Commander's own microphone, in the
        // Commander's own room, with nobody talking. This is the population a synthetic tone can
        // only approximate, and the one a threshold has to be safe against.
        if (Quietest(samples, clip.Format.SampleRate) is { } quiet)
        {
            yield return new Clip("  ^ its quietest 1.0s", "quiet", new Utterance(quiet, clip.Format.SampleRate));
        }
    }

    static float[]? Quietest(float[] samples, int rate)
    {
        var window = rate;

        if (samples.Length < window * 2)
        {
            return null;
        }

        var best = double.MaxValue;
        var bestAt = 0;

        for (var at = 0; at + window <= samples.Length; at += rate / 10)
        {
            var sum = 0d;

            for (var i = at; i < at + window; i++)
            {
                sum += (double)samples[i] * samples[i];
            }

            if (sum < best)
            {
                best = sum;
                bestAt = at;
            }
        }

        return samples[bestAt..(bestAt + window)];
    }
}

string? Arg(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

internal sealed record Clip(string Name, string Kind, Utterance Utterance);
