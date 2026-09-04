using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
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

        Console.WriteLine($"voice {voice}");

        if (!models.Contains(V3, StringComparer.OrdinalIgnoreCase))
        {
            // Not fatal. The listing is what the account may use, and the synthesis calls below
            // are the real test - a model absent from the listing but accepted by the endpoint is
            // itself an answer, and so is a 400 naming the model.
            Console.WriteLine($"NOTE: {V3} is not in this account's model listing.");
        }

        var only = Argument(args, "--only")?.Split(',') ?? ["language", "speed", "latency"];

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
