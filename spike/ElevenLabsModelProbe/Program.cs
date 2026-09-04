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

        if (only.Contains("fields"))
        {
            await VoiceFieldsAsync(voice).ConfigureAwait(false);
        }

        if (only.Contains("headroom"))
        {
            await HeadroomAsync(voice).ConfigureAwait(false);
        }

        if (only.Contains("registers"))
        {
            await RegistersAsync(voice, outputDirectory).ConfigureAwait(false);
        }

        if (only.Contains("grouping"))
        {
            await GroupingAsync(voice, outputDirectory).ConfigureAwait(false);
        }

        if (only.Contains("context"))
        {
            await ContextAsync(voice, outputDirectory).ConfigureAwait(false);
        }

        if (only.Contains("vocabulary"))
        {
            await VocabularyAsync(voice, outputDirectory).ConfigureAwait(false);
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

    // ---- 15. does a voice record say which models suit it ------------------------------------

    /// <summary>
    /// Whether the account's own voice listing carries anything d47 could read to say "this voice
    /// suits v3". ElevenLabs' guidance is that the voice matters more than any other parameter and
    /// that <i>"some tags work well with certain voices while others may not"</i> — which is only
    /// actionable if the fact is on the wire rather than in a web page.
    /// </summary>
    private static async Task VoiceFieldsAsync(string voice)
    {
        Section("15. What a voice record says about the models it suits");

        var (status, body) = await GetAsync($"/voices/{Uri.EscapeDataString(voice)}").ConfigureAwait(false);

        if (status != 200 || body is null)
        {
            Console.WriteLine($"  GET /voices/{voice} answered {status}");
            return;
        }

        using var document = JsonDocument.Parse(body);

        Console.WriteLine("  every field on the record:");

        foreach (var field in document.RootElement.EnumerateObject())
        {
            var shape = field.Value.ValueKind switch
            {
                JsonValueKind.Array => $"[{field.Value.GetArrayLength()}]",
                JsonValueKind.Object => "{...}",
                JsonValueKind.String => $"\"{Head(field.Value.GetString())}\"",
                _ => field.Value.ToString(),
            };

            Console.WriteLine($"    {field.Name,-34} {shape}");
        }

        // The ones that would answer the question, printed whole wherever they exist.
        foreach (var name in new[]
                 {
                     "high_quality_base_model_ids", "verified_languages", "category", "labels",
                     "is_legacy", "is_mixed", "safety_control", "voice_verification",
                 })
        {
            if (document.RootElement.TryGetProperty(name, out var found))
            {
                Console.WriteLine();
                Console.WriteLine($"  {name}: {found}");
            }
        }
    }

    // ---- 14. is there room to group after the first sentence --------------------------------

    /// <summary>
    /// How much audio a request buys per second spent waiting for it. The one number that decides
    /// whether sentences can be grouped after the first without a gap opening in playback.
    /// <para>
    /// The trade looked binary — fast and short, or expressive and slow — and it is not. d47 renders
    /// the next unit <em>while the previous one is playing</em>. So the first unit can stay a single
    /// sentence, which is the whole latency win and is untouched, and everything after it can be as
    /// long as the text allows: by then there is a clip playing to hide the render behind. The only
    /// question is whether the render finishes before the playing clip runs out.
    /// </para>
    /// <para>
    /// That is <c>audio ÷ round trip</c>. Above 1 the group arrives with time to spare and the
    /// grouping is free; below 1 it arrives late and the Commander hears a hole. Measured across the
    /// lengths a group would actually be, because the ratio is not a constant — a long request
    /// returns proportionally more audio for its fixed overhead, which is the effect that makes this
    /// work at all.
    /// </para>
    /// </summary>
    private static async Task HeadroomAsync(string voice, int repeats = 3)
    {
        Section("14. Audio bought per second of waiting, by request size");

        // One sentence, then the sizes a group would plausibly be, up to the splitter's soft cap.
        (string Name, string Text)[] sizes =
        [
            ("1 sentence", "Contact on the scanner, Commander, and it has not seen us yet."),
            ("2 sentences",
                "Contact on the scanner, Commander, and it has not seen us yet. It is holding station "
                + "off the second planet with its drives cold."),
            ("4 sentences",
                "Contact on the scanner, Commander, and it has not seen us yet. It is holding station "
                + "off the second planet with its drives cold, which is either a very patient pilot "
                + "or a very broken one. We have the angle on it for about another minute. After that "
                + "it has the angle on us."),
            ("soft cap",
                "Contact on the scanner, Commander, and it has not seen us yet. It is holding station "
                + "off the second planet with its drives cold, which is either a very patient pilot "
                + "or a very broken one. We have the angle on it for about another minute. After that "
                + "it has the angle on us, and I would rather not find out which of those it is going "
                + "to turn out to be."),
        ];

        Console.WriteLine("  size          chars   round trip      audio    audio/wait");

        foreach (var (name, text) in sizes)
        {
            var trips = new List<double>();
            var audio = 0.0;

            for (var i = 0; i < repeats; i++)
            {
                var spoken = await SpeakAsync(V3, voice, text, "en", speed: 1.0).ConfigureAwait(false);

                if (spoken.Pcm is not { Length: > 0 } pcm)
                {
                    Console.WriteLine($"  {name}: {spoken.Status} {spoken.Said}");
                    trips.Clear();
                    break;
                }

                trips.Add(spoken.Elapsed.TotalMilliseconds);
                audio = Seconds(pcm.Length);
            }

            if (trips.Count == 0)
            {
                continue;
            }

            trips.Sort();
            var trip = trips[trips.Count / 2];

            Console.WriteLine(
                $"  {name,-12} {text.Length,5}   {trip,7:0} ms   {audio,6:0.00}s      "
                + $"{audio / (trip / 1000),5:0.0}x");
        }

        Console.WriteLine();
        Console.WriteLine("  Above 1.0x the next group finishes rendering before the clip playing runs");
        Console.WriteLine("  out, so grouping after the first sentence costs nothing a Commander hears.");
    }

    // ---- 13. the registers a ship AI actually speaks in --------------------------------------

    /// <summary>
    /// Ten tags that appear <em>only</em> inside the prompting guide's example dialogue and in no
    /// list on the page — and which are far closer to what d47 says all day than the sound effects
    /// in the documented set are. A ship AI has no use for <c>[applause]</c> and constant use for
    /// <c>[alarmed]</c>.
    /// <para>
    /// Two things are fixed from the first audition. Each line is written to give its tag something
    /// to act on, because a neutral line cannot show that <c>[dismissive]</c> landed. And each is
    /// left at the length d47 really speaks — 60 to 90 characters — because a tag that only works
    /// at 250 is a tag d47 cannot use, and finding that out is the point rather than a nuisance to
    /// design around.
    /// </para>
    /// </summary>
    private static async Task RegistersAsync(string voice, string outputDirectory)
    {
        Section("13. The registers a ship AI speaks in, at the length it speaks them");

        (string Tag, string Line)[] cases =
        [
            ("alarmed", "Hard lock. Something just cut our drives, and it was not the station."),
            ("panicking", "Hull at 9 percent and the canopy is gone. Get us down, right now."),
            ("reassuring", "Breathe, Commander. Shields are holding and the wing is two jumps out."),
            ("cautiously", "I would not open that cargo hatch until we are well clear of the ring."),
            ("deadpan", "You have arrived. The station is the one that is on fire."),
            ("professional", "Docking granted, pad 14. Gear down, and welcome to Jameson Memorial."),
            ("dismissive", "It is a Sidewinder, Commander. That is not a threat, it is a formality."),
            ("surprised", "That is a Thargoid Interceptor. Here. Twelve light years inside the bubble."),
            ("nervously", "We are four jumps out with no scoop, and I have been doing the arithmetic."),
            ("frustrated", "That is the third time you have plotted us through a permit system."),
        ];

        var directory = Path.Combine(outputDirectory, "registers");
        Directory.CreateDirectory(directory);

        var pieces = new List<byte[]>();
        var gap = new byte[SampleRate * 2 * 2 / 5];

        foreach (var (tag, line) in cases)
        {
            // The control first: the same line with no tag, so what the tag changed is audible
            // rather than remembered. Nothing else in this probe has had that, and it is the
            // difference between "that sounded alarmed" and "that sounded alarmed compared to what".
            var plain = await SpeakAsync(V3, voice, line, "en", speed: 1.0).ConfigureAwait(false);
            var label = await SpeakAsync(V3, voice, $"{tag}.", "en", speed: 1.0).ConfigureAwait(false);
            var read = await SpeakAsync(V3, voice, $"[{tag}] {line}", "en", speed: 1.0)
                .ConfigureAwait(false);

            if (plain.Pcm is not { Length: > 0 } || label.Pcm is not { Length: > 0 }
                || read.Pcm is not { Length: > 0 })
            {
                Console.WriteLine($"  {tag}: {read.Status} {read.Said ?? plain.Said}");
                continue;
            }

            await File.WriteAllBytesAsync(Path.Combine(directory, $"{tag}.wav"), Wav(read.Pcm))
                .ConfigureAwait(false);

            pieces.Add(label.Pcm);
            pieces.Add(gap);
            pieces.Add(plain.Pcm);
            pieces.Add(gap);
            pieces.Add(read.Pcm);
            pieces.Add(gap);

            Console.WriteLine(
                $"  [{tag,-13}] {line.Length,3} characters   "
                + $"plain {Seconds(plain.Pcm.Length):0.00}s   tagged {Seconds(read.Pcm.Length):0.00}s");
        }

        if (pieces.Count == 0)
        {
            return;
        }

        var all = new byte[pieces.Sum(piece => piece.Length)];
        var at = 0;

        foreach (var piece in pieces)
        {
            piece.CopyTo(all, at);
            at += piece.Length;
        }

        var file = Path.Combine(directory, "audition.wav");
        await File.WriteAllBytesAsync(file, Wav(all)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("  Each entry is: the tag named, the line untagged, then the line tagged.");
        Console.WriteLine($"  {file}  {Seconds(all.Length) / 60:0.0} minutes");
    }

    // ---- 12. can a tag survive d47's sentence splitter --------------------------------------

    /// <summary>
    /// The experiment that decides the architecture, and the accent is the instrument because it is
    /// the one tag that either is or is not — no judgement, no neutral line, nothing to argue about.
    /// <para>
    /// Two facts collide. A tag needs a long generation to land at all (ElevenLabs: over 250
    /// characters), and it <b>fades</b> — the accent held for about 186 characters of a 317
    /// character passage and then reverted mid-sentence. Meanwhile <c>SentenceSplitter</c> exists so
    /// speech starts at the first sentence boundary instead of at end of turn, so every synthesis
    /// d47 issues is 23 to 83 characters. Both facts point the same way: d47 sends exactly the shape
    /// v3 handles worst.
    /// </para>
    /// <para>
    /// Four ways to say the same 300 characters, so the cheap fix gets its chance before the
    /// expensive one is argued for:
    /// </para>
    /// <list type="number">
    /// <item><b>whole</b> — one generation, tag at the head. What v3 wants and what d47 does not
    /// do.</item>
    /// <item><b>split-tagged</b> — four short generations, the tag repeated on each. <b>This is the
    /// one that matters</b>: if it works, d47 keeps its sentence boundaries and its latency, and the
    /// injection is per sentence with nothing else to build.</item>
    /// <item><b>split-once</b> — four short generations, tagged only on the first. What naive
    /// injection would produce, and the failure to rule out.</item>
    /// <item><b>whole-repeated</b> — one generation with the tag restated past the decay point, to
    /// see whether a fade can be refreshed in place.</item>
    /// </list>
    /// <para>
    /// The round trip is recorded per variant, because the answer is not only "does it sound right":
    /// grouping sentences buys expression with the one thing the splitter was built to save.
    /// </para>
    /// </summary>
    private static async Task GroupingAsync(string voice, string outputDirectory)
    {
        Section("12. Does a tag survive being split into sentences?");

        const string Tag = "[strong Scottish accent]";

        // Four sentences that read as one passage, so every variant says exactly the same words in
        // the same order and only the seams move.
        string[] sentences =
        [
            "Contact on the scanner, Commander, and it has not seen us yet.",
            "It is holding station off the second planet with its drives cold, which is either a very patient pilot or a very broken one.",
            "We have the angle on it for about another minute.",
            "After that it has the angle on us, and I would rather not find out which it is.",
        ];

        var whole = string.Join(' ', sentences);

        var directory = Path.Combine(outputDirectory, "grouping");
        Directory.CreateDirectory(directory);

        var pieces = new List<byte[]>();
        var gap = new byte[SampleRate * 2 * 2 / 5];

        async Task<(byte[] Pcm, double Milliseconds)?> RunAsync(string name, string said, string[] parts)
        {
            var label = await SpeakAsync(V3, voice, said, "en", speed: 1.0).ConfigureAwait(false);

            var audio = new List<byte[]>();
            var elapsed = 0.0;
            var first = 0.0;

            foreach (var part in parts)
            {
                var read = await SpeakAsync(V3, voice, part, "en", speed: 1.0).ConfigureAwait(false);

                if (read.Pcm is not { Length: > 0 } pcm)
                {
                    Console.WriteLine($"  {name}: {read.Status} {read.Said}");
                    return null;
                }

                audio.Add(pcm);
                elapsed += read.Elapsed.TotalMilliseconds;
                first = first == 0 ? read.Elapsed.TotalMilliseconds : first;
            }

            var joined = audio.SelectMany(part => part).ToArray();

            await File.WriteAllBytesAsync(Path.Combine(directory, name + ".wav"), Wav(joined))
                .ConfigureAwait(false);

            Console.WriteLine(
                $"  {name,-16} {parts.Length} request(s), {parts.Sum(p => p.Length),3} characters   "
                + $"first sound {first:0} ms   all of it {elapsed:0} ms");

            if (label.Pcm is { Length: > 0 } spokenLabel)
            {
                pieces.Add(spokenLabel);
                pieces.Add(gap);
            }

            pieces.Add(joined);
            pieces.Add(gap);

            return (joined, first);
        }

        await RunAsync("whole", "One generation, tagged once at the front.", [$"{Tag} {whole}"])
            .ConfigureAwait(false);

        await RunAsync(
            "split-tagged",
            "Four separate generations, each one tagged.",
            [.. sentences.Select(sentence => $"{Tag} {sentence}")])
            .ConfigureAwait(false);

        await RunAsync(
            "split-once",
            "Four separate generations, only the first one tagged.",
            [.. sentences.Select((sentence, at) => at == 0 ? $"{Tag} {sentence}" : sentence)])
            .ConfigureAwait(false);

        await RunAsync(
            "whole-repeated",
            "One generation, with the tag said again halfway through.",
            [$"{Tag} {sentences[0]} {sentences[1]} {Tag} {sentences[2]} {sentences[3]}"])
            .ConfigureAwait(false);

        if (pieces.Count == 0)
        {
            return;
        }

        var all = new byte[pieces.Sum(piece => piece.Length)];
        var at = 0;

        foreach (var piece in pieces)
        {
            piece.CopyTo(all, at);
            at += piece.Length;
        }

        var file = Path.Combine(directory, "audition.wav");
        await File.WriteAllBytesAsync(file, Wav(all)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"  {file}  {Seconds(all.Length) / 60:0.0} minutes");
    }

    // ---- 11. does the sentence decide whether the tag lands --------------------------------

    /// <summary>
    /// The six tags the Commander heard do nothing, retried two ways, because the audition that
    /// found them had a flaw worth naming: <b>it asked every tag to colour the same deliberately
    /// neutral line.</b> That is the right control for "does this tag exist" and the wrong one for
    /// half of these — sarcasm needs a proposition to contradict and curiosity needs something
    /// unresolved, and "Contact on the scanner. It has not seen us yet." offers neither. A tag with
    /// nothing to act on has no way to show that it landed.
    /// <para>
    /// The second suspect is length, and it is ElevenLabs' own: <i>"very short prompts are more
    /// likely to cause inconsistent outputs"</i>, with prompts <b>greater than 250 characters</b>
    /// encouraged. The audition line was 47. This matters far beyond these six, because d47's
    /// sentence splitter guarantees every synthesis is short — the app operates exactly where v3's
    /// tags are least reliable, by design and not by accident.
    /// </para>
    /// <para>
    /// So each tag is asked twice: once on a short line written to give it something to act on, and
    /// once on the same situation told past 250 characters. Short-and-fitting failing while
    /// long-and-fitting works is a length problem, and a length problem is d47's problem. Both
    /// working says the original line was simply the wrong instrument.
    /// </para>
    /// </summary>
    private static async Task ContextAsync(string voice, string outputDirectory)
    {
        Section("11. The six that did nothing: is it the sentence, or is it the length?");

        var directory = Path.Combine(outputDirectory, "context");
        Directory.CreateDirectory(directory);

        // Each pair says the same thing about the same situation. Only the length differs, and the
        // long one is written the way somebody actually talks rather than padded to a count.
        (string Tag, string Short, string Long)[] cases =
        [
            ("sarcastic",
                "Beautiful landing, Commander. The pad will buff out.",
                "Beautiful landing, Commander. Truly. I have watched a lot of ships come down on that "
                + "pad and I have never seen one do it quite like that. The landing gear is rated for "
                + "a vertical descent, which is a detail I mention only in passing. The pad will buff "
                + "out. Station services have been notified, and they send their regards."),

            ("curious",
                "There is a Corvette on that pad with no registry. Why would anyone park it there?",
                "There is a Federal Corvette sitting on pad 7 with no registry painted anywhere on the "
                + "hull, which is not a thing I have seen before. It is not listed on the arrivals "
                + "board either. Somebody flew a warship into a civilian outpost and then went to "
                + "some trouble to be nobody in particular. Why would anyone do that, Commander?"),

            ("starts laughing",
                "You have plotted a 40 jump route to buy one tonne of biowaste.",
                "Let me read this back to you, Commander, because I want to be certain I have it "
                + "right. You have plotted a forty jump route, at eleven minutes a jump, across two "
                + "sectors of empty sky, to arrive at an outpost whose entire commodity market "
                + "consists of one tonne of biowaste. And you have done this on purpose."),

            ("wheezing",
                "Hull breach on deck three. I am venting atmosphere.",
                "Hull breach on deck three, Commander, and the bulkhead did not seal. I am venting "
                + "atmosphere into the ring and the pressure on that deck is down to nothing. Life "
                + "support is pulling everything it has to hold the rest of the ship. I would very "
                + "much like you to get us somewhere with a landing pad in the next few minutes."),

            ("crying",
                "We lost the whole wing, Commander. Every one of them.",
                "We lost the whole wing, Commander. All four of them, inside ninety seconds, and I "
                + "watched every one of it happen on the scanner and could not do a thing about any "
                + "of it. They were still talking on the channel when the last drive went. I have "
                + "their names. I do not know what you want me to do with them."),

            // The control of the set. An accent does not need the sentence's help, so if this fails
            // both ways it is the voice refusing rather than the line - which is what ElevenLabs
            // means by "the voice needs to be similar enough to the desired delivery".
            ("strong Scottish accent",
                "Contact on the scanner. It has not seen us yet.",
                "Contact on the scanner, Commander, and it has not seen us yet. It is holding station "
                + "off the second planet with its drives cold, which is either a very patient pilot "
                + "or a very broken one. We have the angle on it for about another minute. After "
                + "that it has the angle on us, and I would rather not find out which it is."),
        ];

        var pieces = new List<byte[]>();
        var gap = new byte[SampleRate * 2 * 2 / 5];

        foreach (var (tag, brief, told) in cases)
        {
            foreach (var (which, text) in new[] { ("short", brief), ("long", told) })
            {
                var label = await SpeakAsync(
                    V3, voice, $"{tag}, {which}, {text.Length} characters.", "en", speed: 1.0)
                    .ConfigureAwait(false);

                var read = await SpeakAsync(V3, voice, $"[{tag}] {text}", "en", speed: 1.0)
                    .ConfigureAwait(false);

                if (label.Pcm is not { Length: > 0 } || read.Pcm is not { Length: > 0 })
                {
                    Console.WriteLine($"  {tag} {which}: {read.Status} {read.Said ?? label.Said}");
                    continue;
                }

                await File.WriteAllBytesAsync(
                    Path.Combine(directory, $"{tag.Replace(' ', '-')}-{which}.wav"), Wav(read.Pcm))
                    .ConfigureAwait(false);

                pieces.Add(label.Pcm);
                pieces.Add(gap);
                pieces.Add(read.Pcm);
                pieces.Add(gap);

                Console.WriteLine(
                    $"  {tag,-24} {which,-6} {text.Length,3} characters   {Seconds(read.Pcm.Length):0.00}s");
            }
        }

        if (pieces.Count == 0)
        {
            return;
        }

        var joined = new byte[pieces.Sum(piece => piece.Length)];
        var at = 0;

        foreach (var piece in pieces)
        {
            piece.CopyTo(joined, at);
            at += piece.Length;
        }

        var file = Path.Combine(directory, "audition.wav");
        await File.WriteAllBytesAsync(file, Wav(joined)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"  {file}  {Seconds(joined.Length) / 60:0.0} minutes");
    }

    // ---- 10. which tags actually do anything, on this voice ----------------------------------

    /// <summary>
    /// Every tag ElevenLabs documents, on one neutral line, against a control of the same line with
    /// no tag at all.
    /// <para>
    /// <b>The list d47 lets a model write cannot be the published one.</b> ElevenLabs' own guidance
    /// is that <i>"the voice you choose and its training samples will affect tag effectiveness"</i>,
    /// so a documented tag can be silent on a given voice — and a silent tag is worse than a
    /// missing one, because the spoken-line log then records a delivery that never happened. A
    /// Commander reading "[befuddled]" against a line that was not befuddled is being lied to by
    /// the one record that is supposed to settle complaints.
    /// </para>
    /// <para>
    /// Synthesis is not deterministic, so "did it change anything" needs a noise floor rather than a
    /// comparison against one bare rendition. The control is rendered several times to find the
    /// spread of duration, peak and loudness with nothing asked for; a tag whose renditions land
    /// inside that spread on every axis did nothing worth logging, and is the one to listen to
    /// before it goes on the list.
    /// </para>
    /// </summary>
    private static async Task VocabularyAsync(string voice, string outputDirectory)
    {
        Section("10. Which documented tags do anything on this voice");

        // Neutral on purpose: any delivery could plausibly colour it, so a tag that changes
        // nothing here is not being defeated by a line that fights it.
        const string Line = "Contact on the scanner. It has not seen us yet.";

        var vocabulary = Path.Combine(outputDirectory, "vocabulary");
        Directory.CreateDirectory(vocabulary);

        string[] tags =
        [
            "whispers", "sighs", "exhales", "sarcastic", "curious", "excited", "mischievously",
            "snorts", "laughs", "laughs harder", "starts laughing", "wheezing", "crying", "sings",
            "strong Scottish accent",

            // Not in the documented list, and the one d47 would most want for a danger callout.
            "shouting",
        ];

        var control = new List<(double Seconds, double Peak, double Rms)>();

        for (var i = 0; i < 5; i++)
        {
            if (await MeasureAsync(voice, Line, vocabulary, $"control-{i}").ConfigureAwait(false) is { } one)
            {
                control.Add(one);
            }
        }

        if (control.Count == 0)
        {
            Console.WriteLine("  the control would not render");
            return;
        }

        var span = (
            Low: (control.Min(c => c.Seconds), control.Min(c => c.Peak), control.Min(c => c.Rms)),
            High: (control.Max(c => c.Seconds), control.Max(c => c.Peak), control.Max(c => c.Rms)));

        Console.WriteLine(
            $"  control x{control.Count}: {span.Low.Item1:0.00}-{span.High.Item1:0.00}s   "
            + $"peak {span.Low.Item2:0.0} to {span.High.Item2:0.0} dBFS   "
            + $"rms {span.Low.Item3:0.0} to {span.High.Item3:0.0} dBFS");
        Console.WriteLine();

        foreach (var tag in tags)
        {
            var takes = new List<(double Seconds, double Peak, double Rms)>();

            for (var i = 0; i < 2; i++)
            {
                var name = tag.Replace(' ', '-');

                if (await MeasureAsync(voice, $"[{tag}] {Line}", vocabulary, $"{name}-{i}")
                    .ConfigureAwait(false) is { } one)
                {
                    takes.Add(one);
                }
            }

            if (takes.Count == 0)
            {
                Console.WriteLine($"  [{tag,-24}] would not render");
                continue;
            }

            var inside = takes.All(take =>
                take.Seconds >= span.Low.Item1 && take.Seconds <= span.High.Item1
                && take.Peak >= span.Low.Item2 && take.Peak <= span.High.Item2
                && take.Rms >= span.Low.Item3 && take.Rms <= span.High.Item3);

            Console.WriteLine(
                $"  [{tag,-24}] {string.Join("  ", takes.Select(t => $"{t.Seconds:0.00}s {t.Peak:0.0}/{t.Rms:0.0}"))}"
                + (inside ? "   <- inside the control's spread on every axis" : string.Empty));
        }

        Console.WriteLine();
        Console.WriteLine("  Every tag moves the audio, and that proves less than it looks: [thargoid],");
        Console.WriteLine("  which is nonsense, lengthened the same line by 0.7s. A bracket costs time");
        Console.WriteLine("  whether or not it is honoured, so the numbers cannot tell 'performed' from");
        Console.WriteLine("  'paused'. The audition below is the instrument that can.");

        await AuditionAsync(voice, Line, tags, vocabulary).ConfigureAwait(false);
    }

    /// <summary>
    /// One file, every candidate in it, each read introduced by the tag it was asked for.
    /// <para>
    /// Sixteen separate clips is sixteen decisions to open something; one file is a listen. The
    /// order is fixed and the label is spoken so it can be followed without the screen — and the
    /// first two entries are the reference points: the line with no tag at all, and the line with a
    /// tag that does not exist, which is what "the model did something, but not what was asked"
    /// sounds like.
    /// </para>
    /// </summary>
    private static async Task AuditionAsync(
        string voice,
        string line,
        IReadOnlyList<string> tags,
        string directory)
    {
        Console.WriteLine();
        Console.WriteLine("  Building the audition");

        var pieces = new List<byte[]>();
        var gap = new byte[SampleRate * 2 * 2 / 5];

        (string Said, string Text)[] entries =
        [
            ("No tag at all.", line),
            ("A tag that does not exist.", $"[thargoid] {line}"),
            .. tags.Select(tag => ($"{tag}.", $"[{tag}] {line}")),
        ];

        foreach (var (said, text) in entries)
        {
            // The label is its own request and carries no tag, so it is never part of the
            // performance being judged - the same rule the side-by-side set follows.
            var label = await SpeakAsync(V3, voice, said, "en", speed: 1.0).ConfigureAwait(false);
            var read = await SpeakAsync(V3, voice, text, "en", speed: 1.0).ConfigureAwait(false);

            if (label.Pcm is not { Length: > 0 } || read.Pcm is not { Length: > 0 })
            {
                Console.WriteLine($"    {said} would not render");
                continue;
            }

            pieces.Add(label.Pcm);
            pieces.Add(gap);
            pieces.Add(read.Pcm);
            pieces.Add(gap);
        }

        if (pieces.Count == 0)
        {
            return;
        }

        var joined = new byte[pieces.Sum(piece => piece.Length)];
        var at = 0;

        foreach (var piece in pieces)
        {
            piece.CopyTo(joined, at);
            at += piece.Length;
        }

        var file = Path.Combine(directory, "audition.wav");
        await File.WriteAllBytesAsync(file, Wav(joined)).ConfigureAwait(false);

        Console.WriteLine($"    {file}  {Seconds(joined.Length) / 60:0.0} minutes");
    }

    /// <summary>One rendition, written to disk and measured. Null when the service refused it.</summary>
    private static async Task<(double Seconds, double Peak, double Rms)?> MeasureAsync(
        string voice,
        string text,
        string directory,
        string name)
    {
        var spoken = await SpeakAsync(V3, voice, text, "en", speed: 1.0).ConfigureAwait(false);

        if (spoken.Pcm is not { Length: > 0 } pcm)
        {
            Console.WriteLine($"  {name} {spoken.Status} {spoken.Said}");
            return null;
        }

        var wav = Wav(pcm);
        await File.WriteAllBytesAsync(Path.Combine(directory, name + ".wav"), wav).ConfigureAwait(false);

        var samples = Samples(wav);

        return (
            Seconds(pcm.Length),
            Decibels(samples.Max(Math.Abs)),
            Decibels(Math.Sqrt(samples.Sum(sample => (double)sample * sample) / samples.Length)));
    }

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
