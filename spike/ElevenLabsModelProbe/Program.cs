using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsModelProbe;

/// <summary>
/// Issue 291's spike: is <c>eleven_v3_conversational</c> a model d47 could offer beside the
/// pinned <c>eleven_flash_v2_5</c>?
/// <para>
/// Four questions, in the order that can end the investigation soonest:
/// </para>
/// <list type="number">
/// <item>Does the model exist on this account, and what does the service itself say about it?</item>
/// <item>Does it accept <c>language_code</c> and hold it? Flash was pinned because Multilingual 2
/// rejects the parameter and read a material milestone half in German. If v3 infers the language
/// per line, it is out whatever else it does.</item>
/// <item>Does <c>voice_settings.speed</c> apply, and over what range? ElevenLabs rejects
/// out-of-range rather than clamping, so the rate row's limits are found by being refused.</item>
/// <item>What does a line actually cost in wall-clock, on d47's own sentences, against Flash?
/// The published 75 ms and 280 ms are time-to-first-byte figures for a streaming caller; d47
/// buffers the whole response before a sound is made, so the number that matters is the round
/// trip, measured here the same way for both models.</item>
/// </list>
/// <para>
/// The requests are built by hand rather than through <c>ElevenLabsTtsProvider</c>, because the
/// whole point is to vary the model and that is a <c>const</c> today. They are byte-identical to
/// what the provider sends apart from <c>model_id</c>: same URL, same <c>output_format</c>, same
/// body shape, and the text expanded through the same <see cref="SpokenNumbers"/>.
/// </para>
/// <para>
/// WAVs are written for the two things no assertion can settle - whether English is held, and
/// whether the expressiveness is real on d47's short lines.
/// </para>
/// </summary>
internal static class Program
{
    private const string Host = "https://api.elevenlabs.io/v1";

    private const string Flash = "eleven_flash_v2_5";

    private const string V3 = "eleven_v3_conversational";

    /// <summary>What the provider asks for, and so what this asks for: 24 kHz signed 16-bit mono.</summary>
    private const string OutputFormat = "pcm_24000";

    private const int SampleRate = 24_000;

    /// <summary>
    /// The lines. Numerals and a system name because that is what d47 says all day and what
    /// <see cref="SpokenNumbers"/> rewrites; a German in-game message because that is the exact
    /// input that disqualified Multilingual 2; audio tags because they are the reason to look at
    /// v3 at all, and Flash is sent them too so the control is visible - a model that cannot read
    /// them should say the brackets out loud.
    /// </summary>
    private static readonly (string Name, string Text)[] Lines =
    [
        ("short", "Contact on the scanner."),
        ("system", "Route plotted to Hyades Sector DB-X d1-112, 4 jumps, 88 tonnes of tritium aboard."),
        ("german", "Commander Bergmann says: Achtung, Kopfgeldjager im Ring. Steht das noch, Commander?"),
        ("tags", "[whispers] Contact on the scanner. [sighs] It is a Federal Corvette, and it has seen us."),
    ];

    /// <summary>
    /// The side-by-side set: eight things v3 is said to do that Flash cannot, each written as a
    /// line d47 would really say rather than as a demo sentence.
    /// <para>
    /// Six are audio tags, which is the whole reason to look at v3. The last two carry no tag at
    /// all - one plainly urgent, one with a word in capitals - because a model that is only
    /// expressive when told to is a different proposition from one that reads the sentence. Flash
    /// gets every line unchanged, including the brackets, so what it does with them is part of the
    /// comparison rather than something taken on trust.
    /// </para>
    /// </summary>
    private static readonly (string Name, string Delta, string Text)[] Comparisons =
    [
        ("whisper", "Whispering",
            "[whispers] Cutting the drives. There is something in the next ring, and it has not seen us."),
        ("sigh", "A weary sigh",
            "[sighs] That is the third interdiction this hour, Commander."),
        ("excited", "Excitement",
            "[excited] Double painite hotspot, dead ahead. Both of them inside the ring!"),
        ("urgent", "Shouting under pressure",
            "[shouting] Heat sink, now! Hull at 14 percent!"),
        ("sarcastic", "Sarcasm",
            "[sarcastic] Beautiful landing, Commander. The pad will buff out."),
        ("laughs", "Laughter",
            "[laughs] The entire bounty is 812 credits."),
        ("plain", "Urgency with no tag to tell it to",
            "We just lost the starboard thruster. Get us to the station."),
        ("emphasis", "Emphasis on a capitalised word, with no tag",
            "That is a Federal CORVETTE, not a Viper. Do not interdict it."),
    ];

    /// <summary>
    /// Spoken ahead of each read, by the model doing the reading, in a request of its own. Its own
    /// request because a tag at the head of a v3 generation colours everything after it, and a
    /// label sharing the line would be part of the performance being judged.
    /// <para>
    /// It names the difference being listened for as well as the model, so a file can be played
    /// cold. "Is this better" is not a question anybody can answer; "is this whispered" is.
    /// </para>
    /// <para>
    /// The model is named exactly, because <c>eleven_multilingual_v2</c> is a real model in the
    /// same listing and is the one disqualified in August for reading a milestone half in German. A
    /// file saying "the old V 2" is one careless listen away from being filed as evidence about the
    /// wrong model.
    /// </para>
    /// </summary>
    private static string Label(string model, string delta) =>
        $"{delta}. This is the {(model == V3 ? "new v 3" : "old Flash 2 point 5")}.";

    /// <summary>
    /// The sweep. Wide enough on both sides of the 0.7-1.2 range Flash publishes to find where a
    /// different model's range ends, and dense enough inside it to notice a narrowing.
    /// </summary>
    private static readonly double[] Speeds = [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.5, 2.0];

    /// <summary>
    /// English, German, and no pin at all. <c>de</c> is the control that turns the listening test
    /// into an A/B: a model honouring the parameter says an English sentence in a German accent
    /// and the two files are unmistakably different, and a model ignoring it hands back the same
    /// reading twice. Without that pair, "does it hold English" is a judgement about a single
    /// recording that already sounds English because the words are.
    /// </summary>
    private static readonly string?[] Pins = ["en", "de", null];

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private static HttpClient _http = null!;

    private static string _key = string.Empty;

    private static async Task<int> Main(string[] args)
    {
        var install = Argument(args, "--install") ?? DefaultInstall();
        var voice = Argument(args, "--voice");
        var outputDirectory = Argument(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, "probe-out");
        var repeats = int.TryParse(Argument(args, "--repeats"), out var parsed) ? parsed : 5;

        if (ReadKey(install) is not { Length: > 0 } key)
        {
            Console.Error.WriteLine(
                $"No {ElevenLabsKeySecretName} in {install}\\data\\secrets.json, or it would not decrypt. "
                + "Pass --install <root> for a different d47 install, or set ELEVENLABS_API_KEY.");
            return 1;
        }

        _key = key;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine($"key from {install}\\data\\secrets.json, {key.Length} characters");
        Console.WriteLine($"writing WAVs to {outputDirectory}");

        var models = await ModelsAsync().ConfigureAwait(false);

        voice ??= await FirstVoiceAsync().ConfigureAwait(false);

        if (voice is not { Length: > 0 })
        {
            Console.Error.WriteLine("No voice id: the account listed none and --voice was not given.");
            return 1;
        }

        // Named as well as identified. Every section uses this one voice and varies only the
        // model, so which voice it was is half of what a recording means later.
        Console.WriteLine($"voice {voice} ({await VoiceNameAsync(voice).ConfigureAwait(false) ?? "unnamed"})");

        if (!models.Contains(V3, StringComparer.OrdinalIgnoreCase))
        {
            // Not fatal. The listing is what the account may use, and the synthesis calls below
            // are the real test - a model absent from the listing but accepted by the endpoint is
            // itself an answer, and so is a 400 naming the model.
            Console.WriteLine($"NOTE: {V3} is not in this account's model listing.");
        }

        var only = Argument(args, "--only")?.Split(',') ?? ["language", "speed", "latency", "compare", "plain", "billing"];

        if (only.Contains("language"))
        {
            await LanguageAsync(voice, outputDirectory).ConfigureAwait(false);
        }

        if (only.Contains("speed"))
        {
            await SpeedAsync(voice, 3).ConfigureAwait(false);
        }

        if (only.Contains("latency"))
        {
            await LatencyAsync(voice, repeats).ConfigureAwait(false);
        }

        if (only.Contains("compare"))
        {
            await CompareAsync(voice, outputDirectory, tagged: true).ConfigureAwait(false);
        }

        if (only.Contains("plain"))
        {
            await CompareAsync(voice, outputDirectory, tagged: false).ConfigureAwait(false);
        }

        if (only.Contains("billing"))
        {
            await BillingAsync(voice).ConfigureAwait(false);
        }

        if (only.Contains("unknown"))
        {
            await UnknownTagsAsync(voice, outputDirectory).ConfigureAwait(false);
        }

        if (only.Contains("words"))
        {
            await WordsAsync(Argument(args, "--clips") ?? outputDirectory, install).ConfigureAwait(false);
        }

        Console.WriteLine();
        Console.WriteLine("Listen to the WAVs. The two questions no status code answers are whether");
        Console.WriteLine("the German line is read in English, and whether the tags are performed or spoken.");
        return 0;
    }

    private const string ElevenLabsKeySecretName = "elevenlabs.apiKey";

    // ---- 1. what the service says exists -------------------------------------------------

    private static async Task<HashSet<string>> ModelsAsync()
    {
        Section("1. Models this account may use");

        var (status, body) = await GetAsync("/models").ConfigureAwait(false);

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (status != 200 || body is null)
        {
            Console.WriteLine($"  GET /models answered {status}: {Head(body)}");
            return found;
        }

        using var document = JsonDocument.Parse(body);

        foreach (var model in document.RootElement.EnumerateArray())
        {
            var id = Text(model, "model_id") ?? "?";
            found.Add(id);

            if (id is not (Flash or V3) && !id.Contains("v3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var languages = model.TryGetProperty("languages", out var listed)
                && listed.ValueKind == JsonValueKind.Array
                    ? listed.EnumerateArray().Count()
                    : 0;

            var english = model.TryGetProperty("languages", out var again)
                && again.ValueKind == JsonValueKind.Array
                && again.EnumerateArray().Any(one => Text(one, "language_id") is "en");

            Console.WriteLine($"  {id}");
            Console.WriteLine($"    {Text(model, "name")} - {Text(model, "description")}");
            Console.WriteLine(
                $"    tts={Flag(model, "can_do_text_to_speech")} "
                + $"languages={languages} english={english} "
                + $"style={Flag(model, "can_use_style")} "
                + $"speaker_boost={Flag(model, "can_use_speaker_boost")} "
                + $"max_chars={Number(model, "maximum_text_length_per_request")}");
        }

        Console.WriteLine($"  ({found.Count} models listed in all)");
        return found;
    }

    /// <summary>What the account calls a voice id, for the header line and for the write-up.</summary>
    private static async Task<string?> VoiceNameAsync(string voice)
    {
        var (status, body) = await GetAsync($"/voices/{Uri.EscapeDataString(voice)}").ConfigureAwait(false);

        if (status != 200 || body is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        return Text(document.RootElement, "name");
    }

    private static async Task<string?> FirstVoiceAsync()
    {
        var (status, body) = await GetAsync("/voices").ConfigureAwait(false);

        if (status != 200 || body is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);

        return document.RootElement.TryGetProperty("voices", out var voices)
            ? voices.EnumerateArray()
                .Where(one => Text(one, "name")?.Contains('™', StringComparison.Ordinal) != true)
                .Select(one => Text(one, "voice_id"))
                .FirstOrDefault(id => id is { Length: > 0 })
            : null;
    }

    // ---- 2. language_code ------------------------------------------------------------------

    /// <summary>
    /// The pin's whole reason. Each model is asked for each line twice - with
    /// <c>language_code: en</c> and without - so a rejection of the parameter is told apart from
    /// a rejection of the line, and so there is a control to listen against when the question is
    /// whether the pin is being <em>honoured</em> rather than merely <em>accepted</em>.
    /// </summary>
    private static async Task LanguageAsync(string voice, string outputDirectory)
    {
        Section("2. language_code, accepted and held");

        foreach (var model in new[] { Flash, V3 })
        {
            foreach (var (name, text) in Lines)
            {
                foreach (var pin in Pins)
                {
                    var result = await SpeakAsync(model, voice, text, pin, speed: 1.0)
                        .ConfigureAwait(false);

                    var label = $"{model}-{name}-{pin ?? "unpinned"}";

                    if (result.Pcm is { Length: > 0 })
                    {
                        var file = Path.Combine(outputDirectory, label + ".wav");
                        await File.WriteAllBytesAsync(file, Wav(result.Pcm)).ConfigureAwait(false);

                        Console.WriteLine(
                            $"  {label,-52} {result.Status} "
                            + $"{Seconds(result.Pcm.Length):0.00}s {result.Elapsed.TotalMilliseconds:0} ms");
                    }
                    else
                    {
                        Console.WriteLine($"  {label,-52} {result.Status} {result.Said}");
                    }
                }
            }
        }
    }

    // ---- 3. voice_settings.speed -----------------------------------------------------------

    /// <summary>
    /// Where the rate row's limits come from. Reported as accepted or refused with the service's
    /// own message, because "rejects out of range rather than clamping" is the behaviour the
    /// conversion is written around and a silently clamped value would change what the row means.
    /// <para>
    /// <b>Accepting a value and acting on it are different claims</b>, and only the second one is
    /// what the rate row promises. So the audio's own length is the measurement: a speed that
    /// applies moves it monotonically, and a 200 with a length that does not move is a setting the
    /// model is ignoring. Repeated, because synthesis length varies a little run to run and one
    /// sample of each would let that noise pass for a trend - or hide one.
    /// </para>
    /// </summary>
    private static async Task SpeedAsync(string voice, int repeats)
    {
        Section($"3. voice_settings.speed: refused outside what range, and does it move the audio");

        // The long line, because a rate change is a proportion and there is more of it to see in
        // six seconds than in one.
        var text = Lines[1].Text;

        foreach (var model in new[] { Flash, V3 })
        {
            Console.WriteLine($"  {model}, \"system\" line, median of {repeats}");

            foreach (var speed in Speeds)
            {
                var lengths = new List<double>();
                Spoken last = default;

                for (var i = 0; i < repeats; i++)
                {
                    last = await SpeakAsync(model, voice, text, "en", speed).ConfigureAwait(false);

                    if (last.Pcm is not { Length: > 0 })
                    {
                        break;
                    }

                    lengths.Add(Seconds(last.Pcm.Length));
                }

                if (lengths.Count == 0)
                {
                    Console.WriteLine($"    {speed:0.0}  {last.Status}   {last.Said}");
                    continue;
                }

                lengths.Sort();

                Console.WriteLine(
                    $"    {speed:0.0}  ok    {lengths[lengths.Count / 2]:0.00}s  "
                    + $"({lengths[0]:0.00}-{lengths[^1]:0.00})");
            }
        }
    }

    // ---- 4. what it costs in wall-clock ----------------------------------------------------

    /// <summary>
    /// The round trip, not time to first byte: d47 reads the whole response before the arbiter
    /// is handed a clip, so this is the delay a Commander experiences. Repeated, because one
    /// sample per condition has produced a clean and entirely imaginary effect here before.
    /// Median rather than mean, and the spread is printed, because a single slow call on a shared
    /// account is not a property of the model.
    /// </summary>
    private static async Task LatencyAsync(string voice, int repeats)
    {
        Section($"4. Round trip, {repeats} calls per condition");

        foreach (var (name, text) in Lines.Take(3))
        {
            Console.WriteLine($"  \"{name}\", {text.Length} characters");

            foreach (var model in new[] { Flash, V3 })
            {
                var times = new List<double>();
                var audio = 0.0;

                for (var i = 0; i < repeats; i++)
                {
                    var result = await SpeakAsync(model, voice, text, "en", speed: 1.0).ConfigureAwait(false);

                    if (result.Pcm is not { Length: > 0 })
                    {
                        Console.WriteLine($"    {model,-26} {result.Status} {result.Said}");
                        times.Clear();
                        break;
                    }

                    times.Add(result.Elapsed.TotalMilliseconds);
                    audio = Seconds(result.Pcm.Length);
                }

                if (times.Count == 0)
                {
                    continue;
                }

                times.Sort();

                Console.WriteLine(
                    $"    {model,-26} median {times[times.Count / 2]:0} ms  "
                    + $"({times[0]:0}-{times[^1]:0})  audio {audio:0.00}s");
            }
        }
    }

    // ---- 5. what v3 can do that Flash cannot ------------------------------------------------

    /// <summary>
    /// One WAV per comparison, v3 then Flash, each read introduced by its own model saying which
    /// it is. Back to back in one file rather than two files, because the question is a difference
    /// and a difference is heard in the seam - two players and a click between them is a worse
    /// instrument than four hundred milliseconds of silence.
    /// </summary>
    private static async Task CompareAsync(string voice, string outputDirectory, bool tagged)
    {
        Section(tagged
            ? "5. Side by side, with the tags: v3 first, then Flash, one file each"
            : "6. Side by side, with no tags at all - the lines as d47 writes them today");

        var comparisons = Path.Combine(outputDirectory, tagged ? "compare" : "compare-plain");
        Directory.CreateDirectory(comparisons);

        // Long enough to be a gap rather than a breath, short enough that nobody reaches for the
        // scrub bar between the two halves of one comparison.
        var gap = new byte[SampleRate * 2 * 2 / 5];

        for (var index = 0; index < Comparisons.Length; index++)
        {
            var (name, delta, tags) = Comparisons[index];

            // The same line with the brackets taken out. Nothing in d47 writes a tag today, so
            // this is what a Commander who switched the row would actually hear - and six of the
            // eight lines are only in the tagged set because of a tag, which is why the untagged
            // run is six files rather than eight.
            var text = tagged ? tags : Bare(tags);

            if (!tagged && text == tags)
            {
                continue;
            }

            var pieces = new List<byte[]>();
            var lengths = new List<string>();

            foreach (var model in new[] { V3, Flash })
            {
                var label = await SpeakAsync(
                    model, voice, Label(model, tagged ? delta : "No tags at all"), "en", speed: 1.0)
                    .ConfigureAwait(false);

                var read = await SpeakAsync(model, voice, text, "en", speed: 1.0).ConfigureAwait(false);

                if (label.Pcm is not { Length: > 0 } || read.Pcm is not { Length: > 0 })
                {
                    Console.WriteLine($"  {name,-12} {model,-26} {read.Status} {read.Said ?? label.Said}");
                    continue;
                }

                pieces.Add(label.Pcm);
                pieces.Add(gap);
                pieces.Add(read.Pcm);
                pieces.Add(gap);

                lengths.Add($"{ShortName(model)} {Seconds(read.Pcm.Length):0.00}s");

                // Also on its own, for the comparison somebody wants to loop rather than sit
                // through: the read without the label in front of it.
                await File.WriteAllBytesAsync(
                    Path.Combine(comparisons, $"{name}-{ShortName(model)}{(tagged ? string.Empty : "-plain")}.wav"), Wav(read.Pcm))
                    .ConfigureAwait(false);
            }

            if (pieces.Count == 0)
            {
                continue;
            }

            var joined = new byte[pieces.Sum(piece => piece.Length)];
            var at = 0;

            foreach (var piece in pieces)
            {
                piece.CopyTo(joined, at);
                at += piece.Length;
            }

            var file = Path.Combine(comparisons, $"{index + 1}-{name}{(tagged ? string.Empty : "-plain")}.wav");
            await File.WriteAllBytesAsync(file, Wav(joined)).ConfigureAwait(false);

            Console.WriteLine($"  {index + 1}-{name,-12} {string.Join("  ", lengths),-24} {delta}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {comparisons}");
    }

    private static string ShortName(string model) => model == V3 ? "v3" : "flash";

    /// <summary>The line without its audio tags, and without the double space taking one out leaves.</summary>
    private static string Bare(string text) =>
        TagPattern.Replace(text, string.Empty).Replace("  ", " ", StringComparison.Ordinal).Trim();

    private static readonly Regex TagPattern =
        new(@"\[[a-z ]+\]", RegexOptions.IgnoreCase);

    // ---- 9. did it say all the words, and how loudly -----------------------------------------

    /// <summary>
    /// Reads the clips back. Two failures look the same from a duration and neither is one an ear
    /// settles reliably:
    /// <list type="bullet">
    /// <item><b>Words missing.</b> A tagged line can be <em>longer</em> than the bare one and still
    /// be short of half its words, because the performance the tag asks for costs time of its own.
    /// So the words are counted rather than inferred, by the transcriber d47 already ships.</item>
    /// <item><b>Words present but inaudible.</b> A whisper that comes back 20 dB down is a line the
    /// Commander does not hear over Elite, which is indistinguishable from a line that was never
    /// said - and it is a level, so it is a number.</item>
    /// </list>
    /// <para>
    /// tiny.en is the model in the install, and it is a check rather than an authority: a word it
    /// misses is a word worth listening for, not proof of anything. What it is good for is
    /// <em>relative</em> - the same sentence four ways, where only the brackets changed.
    /// </para>
    /// </summary>
    private static async Task WordsAsync(string clipDirectory, string install)
    {
        Section("9. Every word, and how loud it came back");

        var model = Path.Combine(install, "data", "models", "ggml-tiny.en.bin");

        if (!File.Exists(model))
        {
            Console.WriteLine($"  no transcriber model at {model}; levels only");
        }

        using var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

        var loaded = File.Exists(model) && transcriber.Load(model, "tiny.en", useGpu: false);

        if (File.Exists(model) && !loaded)
        {
            Console.WriteLine($"  {transcriber.Unavailable}");
        }

        foreach (var file in Directory.GetFiles(clipDirectory, "*.wav").OrderBy(name => name))
        {
            var samples = Samples(await File.ReadAllBytesAsync(file).ConfigureAwait(false));

            var peak = samples.Length == 0 ? 0 : samples.Max(Math.Abs);
            var rms = samples.Length == 0
                ? 0
                : Math.Sqrt(samples.Sum(sample => (double)sample * sample) / samples.Length);

            Console.WriteLine($"  {Path.GetFileNameWithoutExtension(file)}");
            Console.WriteLine(
                $"    {samples.Length / (double)SampleRate:0.00}s   peak {Decibels(peak):0.0} dBFS   "
                + $"rms {Decibels(rms):0.0} dBFS");

            if (!loaded)
            {
                continue;
            }

            var heard = await transcriber
                .TranscribeAsync(new Utterance(Downsample(samples), 16_000), [])
                .ConfigureAwait(false);

            Console.WriteLine($"    heard: {heard.Text}");
        }
    }

    private static double Decibels(double amplitude) =>
        amplitude <= 0 ? double.NegativeInfinity : 20 * Math.Log10(amplitude);

    /// <summary>The WAV's samples as floats in -1..1, skipping the 44-byte header this probe wrote.</summary>
    private static float[] Samples(byte[] wav)
    {
        const int Header = 44;

        if (wav.Length <= Header)
        {
            return [];
        }

        var samples = new float[(wav.Length - Header) / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(wav, Header + (i * 2)) / 32768f;
        }

        return samples;
    }

    /// <summary>
    /// 24 kHz to the 16 kHz Whisper wants, linearly. Good enough for reading words back; this is
    /// not in the product and nothing is judged on its fidelity.
    /// </summary>
    private static float[] Downsample(float[] samples)
    {
        var ratio = SampleRate / 16_000.0;
        var wanted = (int)(samples.Length / ratio);
        var output = new float[wanted];

        for (var i = 0; i < wanted; i++)
        {
            var at = i * ratio;
            var left = (int)at;
            var right = Math.Min(left + 1, samples.Length - 1);

            output[i] = (float)(samples[left] + ((samples[right] - samples[left]) * (at - left)));
        }

        return output;
    }

    // ---- 8. what a tag the model does not know does ------------------------------------------

    /// <summary>
    /// The question that decides whether d47 may let a model write its own tags, or must hand it a
    /// list. ElevenLabs documents which tags exist and documents nothing about what happens to one
    /// that does not — and the failure that matters is not "the tag is ignored", it is <b>the word
    /// inside the brackets being read out loud</b>, which a Commander hears as d47 saying
    /// "grumbles" in the middle of a sentence.
    /// <para>
    /// Four renditions of one line, identical but for the brackets: none, a documented tag, an
    /// undocumented but plausible one, and a nonsense one. Durations are printed but do not settle
    /// it; the files are the instrument.
    /// </para>
    /// </summary>
    private static async Task UnknownTagsAsync(string voice, string outputDirectory)
    {
        Section("8. A documented tag, an undocumented one, and a nonsense one");

        var unknown = Path.Combine(outputDirectory, "unknown-tags");
        Directory.CreateDirectory(unknown);

        (string Name, string Text)[] cases =
        [
            ("none", "Cutting the drives. It has not seen us."),
            ("documented", "[whispers] Cutting the drives. It has not seen us."),
            ("undocumented", "[grumbles quietly] Cutting the drives. It has not seen us."),
            ("nonsense", "[thargoid] Cutting the drives. It has not seen us."),
        ];

        foreach (var (name, text) in cases)
        {
            var spoken = await SpeakAsync(V3, voice, text, "en", speed: 1.0).ConfigureAwait(false);

            if (spoken.Pcm is not { Length: > 0 })
            {
                Console.WriteLine($"  {name,-14} {spoken.Status} {spoken.Said}");
                continue;
            }

            await File.WriteAllBytesAsync(Path.Combine(unknown, $"{name}.wav"), Wav(spoken.Pcm))
                .ConfigureAwait(false);

            Console.WriteLine($"  {name,-14} {Seconds(spoken.Pcm.Length):0.00}s   {text}");
        }

        Console.WriteLine();
        Console.WriteLine("  If a bracketed word is ever heard, the model may not write its own tags.");
    }

    // ---- 7. are the tags billed --------------------------------------------------------------

    /// <summary>
    /// Whether the bracketed words cost characters. The published price is per character and no
    /// page says whether a tag is one, which matters because d47's spend ledger counts what it
    /// puts on the wire: if tags are billed, injecting them raises the bill by however many
    /// characters they run to and the ledger is already right; if they are free, the ledger would
    /// over-count every tagged line.
    /// <para>
    /// Measured off the account's own meter rather than reasoned about - <c>character_count</c>
    /// before and after one synthesis, which is the only number that settles it. Run when nothing
    /// else is speaking on the account, since the meter is the account's and not this process's.
    /// </para>
    /// </summary>
    private static async Task BillingAsync(string voice)
    {
        Section("7. Are audio tags billed as characters");

        const string Tagged = "[whispers] Cutting the drives. [sighs] It has not seen us.";
        var bare = Bare(Tagged);

        var taggedLength = SpokenNumbers.Expand(Tagged).Length;
        var bareLength = SpokenNumbers.Expand(bare).Length;

        Console.WriteLine($"  tagged  {taggedLength} characters on the wire");
        Console.WriteLine($"  bare    {bareLength} characters on the wire");
        Console.WriteLine($"  the tags are worth {taggedLength - bareLength}");
        Console.WriteLine();

        foreach (var (label, text) in new[] { ("tagged", Tagged), ("bare", bare) })
        {
            var before = await MeterAsync().ConfigureAwait(false);

            if (before is null)
            {
                Console.WriteLine("  the account meter would not read; skipping");
                return;
            }

            var spoken = await SpeakAsync(V3, voice, text, "en", speed: 1.0).ConfigureAwait(false);

            if (spoken.Pcm is not { Length: > 0 })
            {
                Console.WriteLine($"  {label} {spoken.Status} {spoken.Said}");
                continue;
            }

            var after = await MeterAsync().ConfigureAwait(false);

            Console.WriteLine(
                $"  {label,-8} meter {before} -> {after}, charged {after - before} "
                + $"for {SpokenNumbers.Expand(text).Length} characters sent");
        }
    }

    /// <summary>The account's used-character count, which is the only meter either side agrees on.</summary>
    private static async Task<int?> MeterAsync()
    {
        var (status, body) = await GetAsync("/user/subscription").ConfigureAwait(false);

        if (status != 200 || body is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);

        return document.RootElement.TryGetProperty("character_count", out var used)
            && used.ValueKind == JsonValueKind.Number
                ? used.GetInt32()
                : null;
    }

    // ---- the wire --------------------------------------------------------------------------

    private readonly record struct Spoken(int Status, byte[]? Pcm, string? Said, TimeSpan Elapsed);

    /// <summary>
    /// One synthesis, built the way <c>ElevenLabsTtsProvider.SynthesizeAsync</c> builds one -
    /// same URL and output format, same body shape, text through the same expander - with only
    /// the model, the language pin and the speed varied.
    /// </summary>
    private static async Task<Spoken> SpeakAsync(
        string model,
        string voice,
        string text,
        string? language,
        double speed)
    {
        var body = new Dictionary<string, object>
        {
            ["text"] = SpokenNumbers.Expand(text),
            ["model_id"] = model,
            ["voice_settings"] = new Dictionary<string, object> { ["speed"] = speed },
        };

        if (language is not null)
        {
            body["language_code"] = language;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Host}/text-to-speech/{Uri.EscapeDataString(voice)}?output_format={OutputFormat}");

        request.Headers.Add("xi-api-key", _key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, Pretty), Encoding.UTF8, "application/json");

        var clock = Stopwatch.StartNew();

        try
        {
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            clock.Stop();

            return response.IsSuccessStatusCode
                ? new Spoken((int)response.StatusCode, bytes, null, clock.Elapsed)
                : new Spoken((int)response.StatusCode, null, Said(bytes), clock.Elapsed);
        }
        catch (Exception error)
        {
            clock.Stop();
            return new Spoken(0, null, error.Message, clock.Elapsed);
        }
    }

    private static async Task<(int Status, string? Body)> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Host + path);
        request.Headers.Add("xi-api-key", _key);

        try
        {
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        catch (Exception error)
        {
            return (0, error.Message);
        }
    }

    /// <summary>The <c>detail.message</c> ElevenLabs puts in an error body, or the head of it.</summary>
    private static string Said(byte[] raw)
    {
        var body = Encoding.UTF8.GetString(raw);

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("message", out var said)
                    ? said.GetString() ?? Head(body)
                    : detail.ToString();
            }
        }
        catch (JsonException)
        {
            // A body that will not parse is still worth showing.
        }

        return Head(body);
    }

    // ---- odds and ends ---------------------------------------------------------------------

    /// <summary>
    /// The key, read the way d47 reads it. DPAPI, current user, so this only works as the
    /// Commander who stored it - which is the point: no key is typed, pasted or printed.
    /// </summary>
    private static string? ReadKey(string install)
    {
        if (Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY") is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        var store = new SecretStore(
            new AppPaths(install), new DpapiSecretProtector(), NullLogger<SecretStore>.Instance);

        return store.TryGet(ElevenLabsKeySecretName, out var key) ? key : null;
    }

    /// <summary>
    /// The Debug install if it is there, otherwise the published one. A spike's own
    /// <c>AppContext.BaseDirectory</c> is never right: it carries no <c>DevInstallRoot</c>, so
    /// <c>AppPaths.ForRunningBuild</c> would point at this probe's bin folder.
    /// </summary>
    private static string DefaultInstall()
    {
        var here = AppContext.BaseDirectory;

        for (var directory = new DirectoryInfo(here); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "dev-install");

            if (Directory.Exists(Path.Combine(candidate, "data")))
            {
                return candidate;
            }
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "d47");
    }

    private static byte[] Wav(byte[] pcm)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();

        return memory.ToArray();
    }

    private static double Seconds(int bytes) => bytes / 2.0 / SampleRate;

    private static string? Argument(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()
            : null;

    private static string Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) ? found.ValueKind.ToString() : "?";

    private static string Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Number
            ? found.GetInt32().ToString(CultureInfo.InvariantCulture)
            : "?";

    private static string Head(string? body) =>
        body is null ? string.Empty : body.Length <= 200 ? body : body[..200] + "…";

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));
    }
}
