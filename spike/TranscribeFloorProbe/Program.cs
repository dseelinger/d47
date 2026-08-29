// What does one transcription actually cost, and which part of it is the name-hint prompt?
//
// #182 measured the installed build's log and found every utterance costing about three seconds
// whatever the device and whatever the audio length. This drives WhisperTranscriber directly, with
// the same clips and the same model, so the candidate causes can be told apart:
//
//   hints    -- the same clip at 0, 5, 20, 40 and 60 name hints, steady state
//   rebuild  -- what a *changed* hint set costs, since a new prompt means a new processor
//   length   -- the same clip padded to a spread of durations, at 0 hints
//   knobs    -- thread count and probability accumulation, straight against Whisper.net
//   budget   -- hints past the cap, to find where Whisper stops reading the prompt
//
//   dotnet run --project spike/TranscribeFloorProbe -c Release -- --reps 5 --out report.md
//
// Throwaway. See spike/README.md.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using D47.Core.Audio;
using D47.Core.Knowledge;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Whisper.net;

var installed = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Programs",
    "d47");

var modelPath = Arg("--model") ?? Path.Combine(installed, "data", "models", "ggml-small.en.bin");
var clipDirectory = Arg("--clips") ?? Path.Combine(installed, "data", "flight");
var reps = int.Parse(Arg("--reps") ?? "5", CultureInfo.InvariantCulture);
var maxClips = int.Parse(Arg("--max-clips") ?? "99", CultureInfo.InvariantCulture);
var sections = (Arg("--sections") ?? "hints,rebuild,length,knobs").Split(',');
var useGpu = Array.Exists(args, a => a == "--gpu");
var report = Arg("--out");

var model = Path.GetFileNameWithoutExtension(modelPath).Replace("ggml-", string.Empty, StringComparison.Ordinal);

Console.WriteLine($"model  {modelPath}");
Console.WriteLine($"clips  {clipDirectory}");
Console.WriteLine($"reps   {reps}   device {(useGpu ? "gpu" : "cpu")}   sections {string.Join(',', sections)}");
Console.WriteLine();

var clips = Clips(clipDirectory).Take(maxClips).ToList();

if (clips.Count == 0)
{
    Console.Error.WriteLine("No *-heard.wav clips found. Pass --clips <dir>.");
    return 1;
}

using var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

if (!transcriber.Load(modelPath, model, useGpu))
{
    Console.Error.WriteLine(transcriber.Unavailable);
    return 1;
}

Console.WriteLine($"loaded, UsingGpu={transcriber.UsingGpu}");
Console.WriteLine();

var lines = new StringBuilder();

// The very first call pays for whatever the native side does once — mel tables, the thread pool,
// the first allocation of the KV cache. Excluded from every figure below rather than averaged in.
await transcriber.TranscribeAsync(clips[0].Utterance, Hints(0));

int[] counts = [0, 5, 20, 40, 60];

if (sections.Contains("hints"))
{
    Say($"## Hint sweep — {model}, steady state");
    Say();
    Say("| Clip | Audio | " + string.Join(" | ", counts.Select(c => $"{c} hints")) + " |");
    Say("|---|---|" + string.Concat(counts.Select(_ => "---|")));

    foreach (var clip in clips)
    {
        var cells = new List<string>();

        foreach (var count in counts)
        {
            var hints = Hints(count);

            // Steady state: the processor is already built for these names before the clock
            // starts, so this is inference cost and not the rebuild the next section measures.
            await transcriber.TranscribeAsync(clip.Utterance, hints);

            cells.Add(Cell(await Runs(clip, hints)));
        }

        Say($"| {clip.Name} | {clip.Utterance.Duration.TotalSeconds:0.0}s | {string.Join(" | ", cells)} |");
    }

    Say();
}

if (sections.Contains("rebuild"))
{
    Say($"## Rebuild — what a *changed* hint set costs on top ({model})");
    Say();
    Say("| Clip | Hints unchanged | Hints changed every call | Difference |");
    Say("|---|---|---|---|");

    foreach (var clip in clips)
    {
        var steadyHints = Hints(40);
        await transcriber.TranscribeAsync(clip.Utterance, steadyHints);
        var steady = await Runs(clip, steadyHints);

        // A different list each time, so Prime rebuilds the processor on every call. Same length,
        // so the only thing that changed is that it changed.
        var churned = new List<double>();

        for (var i = 0; i < reps; i++)
        {
            churned.Add(await Once(clip, Hints(40, salt: i + 1)));
        }

        Say($"| {clip.Name} | {Cell(steady)} | {Cell(churned)} | {Median(churned) - Median(steady):+0;-0} ms |");
    }

    Say();
}

var longest = clips.MaxBy(c => c.Utterance.Samples.Length)!;

if (sections.Contains("length"))
{
    Say($"## Length sweep — one clip padded with silence, 0 hints ({model})");
    Say();
    Say("| Audio | Time |");
    Say("|---|---|");

    foreach (var seconds in new[] { 0.25, 1, 2, 5, 10, 20, 29, 31, 45, 61 })
    {
        var padded = new Clip($"{seconds}s", Resize(longest.Utterance, seconds));
        await transcriber.TranscribeAsync(padded.Utterance, Hints(0));

        Say($"| {seconds:0.##}s | {Cell(await Runs(padded, Hints(0)))} |");
    }

    Say();
}

if (sections.Contains("budget"))
{
    // Whisper's initial prompt is not unbounded: whisper.cpp keeps the last `n_text_ctx / 2`
    // tokens of it and drops the rest. If that is what happens, the cost of adding hints stops
    // rising at the point the cap bites — and every hint past it is being paid for in nothing.
    Say($"## Prompt budget — where adding hints stops costing anything ({model})");
    Say();
    Say("| Hints | Prompt | Time | Transcript |");
    Say("|---|---|---|---|");

    foreach (var count in new[] { 0, 20, 40, 60, 80, 120, 200, 400 })
    {
        var hints = Hints(count);
        var prompt = string.Join(", ", hints);

        await transcriber.TranscribeAsync(longest.Utterance, hints);

        var times = await Runs(longest, hints);
        var heard = await transcriber.TranscribeAsync(longest.Utterance, hints);

        Say($"| {count} | {prompt.Length} chars | {Cell(times)} | {heard.Text} |");
    }

    Say();
}

if (sections.Contains("knobs"))
{
    // Straight against Whisper.net rather than through the transcriber: these are builder settings
    // d47 does not currently set, and the question is what setting them would buy.
    transcriber.Unload();

    using var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = useGpu });

    Say($"## Builder knobs — {model}, {longest.Utterance.Duration.TotalSeconds:0.0}s clip, 40 hints");
    Say();
    Say($"`Environment.ProcessorCount` here is {Environment.ProcessorCount}. Whisper's own default is "
        + "`min(4, hardware_concurrency)`, so the unset row is four threads.");
    Say();
    Say("| Setting | Time | Transcript |");
    Say("|---|---|---|");

    var prompt = string.Join(", ", Hints(40));

    foreach (var (label, configure) in Knobs(prompt))
    {
        using var processor = configure(factory.CreateBuilder().WithLanguage("en").WithPrompt(prompt)).Build();

        // One warm call before the clock, same as every other section.
        var heard = await Drain(processor, longest.Utterance);

        var times = new List<double>();

        for (var i = 0; i < reps; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await Drain(processor, longest.Utterance);
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // The transcript beside the time, because a thread count that changed what was heard
        // would be a different setting rather than a faster one — ggml reduces across threads,
        // so the arithmetic is not bit-identical and the question is fair.
        Say($"| {label} | {Cell(times)} | {heard} |");
    }

    Say();
}

Console.WriteLine();
Console.WriteLine(lines.ToString());

if (report is not null)
{
    File.WriteAllText(report, lines.ToString());
    Console.WriteLine($"written to {report}");
}

return 0;

void Say(string line = "")
{
    lines.AppendLine(line);
}

async Task<List<double>> Runs(Clip clip, IReadOnlyList<string> hints)
{
    var times = new List<double>();

    for (var i = 0; i < reps; i++)
    {
        times.Add(await Once(clip, hints));
    }

    return times;
}

async Task<double> Once(Clip clip, IReadOnlyList<string> hints)
{
    var stopwatch = Stopwatch.StartNew();
    await transcriber.TranscribeAsync(clip.Utterance, hints);
    return stopwatch.Elapsed.TotalMilliseconds;
}

static async Task<string> Drain(WhisperProcessor processor, Utterance utterance)
{
    var text = new StringBuilder();

    await foreach (var segment in processor.ProcessAsync(utterance.Samples))
    {
        text.Append(segment.Text);
    }

    return text.ToString().Trim();
}

static List<(string Label, Func<WhisperProcessorBuilder, WhisperProcessorBuilder> Configure)> Knobs(string prompt) =>
[
    ("what d47 ships (`WithProbabilities`, threads unset)", b => b.WithProbabilities()),
    ("no `WithProbabilities`", b => b),
    ("`WithThreads(4)`", b => b.WithProbabilities().WithThreads(4)),
    ("`WithThreads(8)`", b => b.WithProbabilities().WithThreads(8)),
    ("`WithThreads(12)`", b => b.WithProbabilities().WithThreads(12)),
    ("`WithThreads(16)`", b => b.WithProbabilities().WithThreads(16)),
    ("`WithThreads(24)`", b => b.WithProbabilities().WithThreads(24)),
    ("`WithThreads(16)`, no prompt", b => b.WithProbabilities().WithThreads(16).WithPrompt(string.Empty)),
];

// Median rather than mean, and the spread beside it: one scheduling hiccup on a desktop machine
// moves a mean of five and moves a median of five not at all.
static string Cell(List<double> times) =>
    $"{Median(times):0} ms ({times.Min():0}-{times.Max():0})";

static double Median(List<double> times)
{
    var sorted = times.Order().ToList();
    return sorted.Count % 2 == 1
        ? sorted[sorted.Count / 2]
        : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
}

// Real Elite proper nouns of the shape the app actually sends: engineers, systems, stations and
// ship names. Salted variants differ only in a digit, so a churn run changes the prompt without
// changing its length.
static IReadOnlyList<string> Hints(int count, int salt = 0)
{
    if (count == 0)
    {
        return [];
    }

    var pool = new List<string>
    {
        "Shinrarta Dezhra", "Jameson Memorial", "ICZ OX-U b2-1", "Deciat", "Farseer Inc",
        "Maia", "Colonia", "Sagittarius A*", "Robigo Mines", "Diaguandri",
        "Ray Gateway", "Hutton Orbital", "Alpha Centauri", "Beagle Point", "Sothis",
        "Ceos", "Jacques Station", "Explorer's Anchorage", "LHS 3447", "Eravate",
    };

    pool.AddRange(EngineerDirectory.All.Select(engineer => engineer.Name));

    // Distinct past the end of the pool rather than repeating it: a prompt of the same twenty
    // names over and over would compress, and the question the budget sweep asks is about how
    // many *different* names fit.
    return
    [
        .. Enumerable
            .Range(0, count)
            .Select(i => (i < pool.Count ? pool[i] : $"{pool[i % pool.Count]} {i / pool.Count}")
                + (salt == 0 ? string.Empty : $" {salt}")),
    ];
}

static Utterance Resize(Utterance source, double seconds)
{
    var wanted = (int)(seconds * source.SampleRate);
    var samples = new float[wanted];

    // Trimmed, or the speech followed by silence — which is what a real utterance is, since the
    // gate closes on a settle and the tail is quiet by definition.
    source.Samples.AsSpan(0, Math.Min(wanted, source.Samples.Length)).CopyTo(samples);

    return new Utterance(samples, source.SampleRate);
}

static List<Clip> Clips(string directory)
{
    if (!Directory.Exists(directory))
    {
        return [];
    }

    return
    [
        .. Directory
            .EnumerateFiles(directory, "*-heard.wav")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var clip = WavReader.Read(path);
                return new Clip(Path.GetFileNameWithoutExtension(path), Utterance(clip));
            }),
    ];

    static Utterance Utterance(AudioClip clip)
    {
        var pcm = clip.Pcm.Span;
        var samples = new float[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        return new Utterance(samples, clip.Format.SampleRate);
    }
}

string? Arg(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

internal sealed record Clip(string Name, Utterance Utterance);
