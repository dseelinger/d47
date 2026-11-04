using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Tts.Tests.ElevenLabsProbeSupport;

namespace D47.Tts.Tests;

/// <summary>
/// A one-off live measurement of ElevenLabs sound generation (SFX v3) and the <c>audio_effects</c> field of
/// text to speech. Spends real credits, so it runs only with <c>D47_ELEVENLABS_PROBE=1</c> as well as the key.
/// <c>D47_ELEVENLABS_PROBE_PARTS</c> is <c>1</c>, <c>2</c> or <c>12</c> (default).
/// </summary>
[Trait("Category", "Integration")]
public class TheSoundEffectsAndAudioEffectsProbeWritesItsReportTests
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";
    private const string SoundModel = "eleven_text_to_sound_v3";
    private const int SoundRate = 48000;
    private const int SpeechRate = 24000;
    private const string SpeechText = "Shields are down, Commander. Boost and get clear of the fight.";

    private const string CueStyle = "Short, clean, synthetic ship-computer tone, like a cockpit interface chime. No voice, no reverb tail.";
    private const string AlertStyle = "Urgent but not harsh synthetic ship-computer alert, clean electronic tones. No voice.";

    private sealed record Sound(string Folder, string Name, string Prompt, double Seconds, bool Loop = false);

    private static readonly Sound[] Alerts =
    [
        new("alerts", "interdiction", "an interdiction warning: two rising urgent electronic pulses", 1.5),
        new("alerts", "piracy", "a piracy warning: a low pulsing electronic two-tone alarm", 1.5),
        new("alerts", "bountyhunter", "a bounty hunter warning: three sharp descending electronic pings", 1.5),
        new("alerts", "underfire", "an under-fire warning: rapid urgent electronic beeps", 1.5),
        new("alerts", "overheating", "an overheating warning: a slow rising electronic whine with a beep", 1.5),
        new("alerts", "rivalterritory", "a rival territory warning: a cautious two-note electronic alert", 1.5),
        new("alerts", "timerelapsed", "a timer elapsed notice: a clear electronic bell of three notes", 1.5),
    ];

    private static readonly Sound[] Cues =
    [
        new("cues", "answered", "a confirmation: one soft rising two-note chime", 0.6),
        new("cues", "failed", "a failure: a low short falling two-note tone", 0.7),
        new("cues", "idle", "an idle: a quiet single soft tone", 0.5),
        new("cues", "listening", "a listening: a short bright rising blip", 0.5),
        new("cues", "speaking", "a speaking: a short soft descending blip", 0.5),
        new("cues", "thinking", "a thinking: two quick soft ticks", 0.6),
        new("cues", "transcribing", "a transcribing: three very quick soft ticks", 0.6),
        new("cues", "unsure", "an unsure: a short questioning rising and falling tone", 0.7),
    ];

    private static readonly Sound[] Beds =
    [
        new("beds", "thinking-hum", "a quiet continuous electronic hum of a ship computer thinking, steady and unobtrusive", 4, Loop: true),
    ];

    private static readonly Sound[] Ambience =
    [
        new("ambience", "station-interior", "ambience of a quiet space station interior: distant machinery, air handling, low hum", 10, Loop: true),
        new("ambience", "hangar", "ambience of a large hangar bay: deep echoing machinery, distant clanks, ventilation", 10, Loop: true),
        new("ambience", "cockpit-supercruise", "ambience of a spaceship cockpit in supercruise: steady low engine drone, faint instrument beeps", 10, Loop: true),
    ];

    private static string? Key => Environment.GetEnvironmentVariable("D47_ELEVENLABS_KEY");

    [Fact]
    public async Task TheProbeWritesItsReport()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key)
                || Environment.GetEnvironmentVariable("D47_ELEVENLABS_PROBE") != "1",
            "set D47_ELEVENLABS_KEY and D47_ELEVENLABS_PROBE=1 to run the paid sound and audio-effects probe");

        var parts = Environment.GetEnvironmentVariable("D47_ELEVENLABS_PROBE_PARTS") is { Length: > 0 } p ? p : "12";

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var token = timeout.Token;

        var voice = Environment.GetEnvironmentVariable("D47_ELEVENLABS_VOICE");

        if (string.IsNullOrWhiteSpace(voice))
        {
            using var provider = new ElevenLabsTtsProvider(() => Key, NullLogger<ElevenLabsTtsProvider>.Instance);
            var voices = (await provider.ListVoicesAsync(token)).Voices;
            Assert.NotEmpty(voices);
            voice = voices[0].Id;
        }

        var folder = Path.Combine(
            Path.GetTempPath(),
            "d47-elevenlabs-probe",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        var probe = new Probe(http, Key!, voice, folder);

        await probe.RunAsync(parts, token);

        var report = Path.Combine(folder, "report.md");
        await File.WriteAllTextAsync(report, probe.Report.ToString(), token);

        TestContext.Current.TestOutputHelper?.WriteLine($"ElevenLabs sound and audio-effects probe written to {folder}");
        Assert.True(File.Exists(report));
    }

    private sealed record Outcome(
        string Endpoint, string Model, string Format, string Body, int Status, string? Detail,
        byte[] Audio, int SampleRate, int Channels, long? Credits);

    private sealed class Probe(HttpClient http, string key, string voice, string folder)
    {
        public StringBuilder Report { get; } = new();

        public async Task RunAsync(string parts, CancellationToken token)
        {
            Report.AppendLine("# ElevenLabs sound effects and audio effects probe");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture, $"- Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Report.AppendLine(CultureInfo.InvariantCulture, $"- Voice: `{voice}`");
            Report.AppendLine("- Credits are `character_count` from `GET /v1/user/subscription`, read around each request; the counter can update late.");
            Report.AppendLine();

            var start = await CharacterCountAsync(token);

            long? afterOne = start;

            if (parts.Contains('1', StringComparison.Ordinal))
            {
                await SoundsAsync(token);
                afterOne = await CharacterCountAsync(token);
                Report.AppendLine(CultureInfo.InvariantCulture, $"**Part 1 credits used: {Delta(start, afterOne)}**");
                Report.AppendLine();
            }

            if (parts.Contains('2', StringComparison.Ordinal))
            {
                await EffectsAsync(token);
                var end = await CharacterCountAsync(token);
                Report.AppendLine(CultureInfo.InvariantCulture, $"**Part 2 credits used: {Delta(afterOne, end)}**");
                Report.AppendLine();
                Report.AppendLine(CultureInfo.InvariantCulture, $"**Total credits used: {Delta(start, end)}**");
            }
            else
            {
                Report.AppendLine(CultureInfo.InvariantCulture, $"**Total credits used: {Delta(start, afterOne)}**");
            }
        }

        private async Task SoundsAsync(CancellationToken token)
        {
            Report.AppendLine("## Part 1. Sound effects");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture,
                $"`POST {BaseUrl}/sound-generation?output_format=wav_{SoundRate}`, body `text`, `model_id` (`{SoundModel}`), `duration_seconds`, `loop`. "
                + $"Cue style prompt: \"{CueStyle}\" Alert style prompt: \"{AlertStyle}\"");
            Report.AppendLine();
            Report.AppendLine("| file | prompt | seconds asked | loop | status | seconds | credits |");
            Report.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            var assets = AssetsFolder();

            foreach (var sound in Alerts.Concat(Cues).Concat(Beds).Concat(Ambience))
            {
                var shipped = assets is null ? null : Path.Combine(assets, sound.Folder, sound.Name + ".wav");

                if (shipped is not null && File.Exists(shipped))
                {
                    File.Copy(shipped, Path.Combine(folder, $"{sound.Folder}-{sound.Name}.shipped.wav"), overwrite: true);
                }

                var style = sound.Folder switch
                {
                    "alerts" => AlertStyle,
                    "cues" => CueStyle,
                    "beds" => "Clean synthetic ship-computer texture.",
                    _ => "Science-fiction setting, no music, no voices.",
                };

                foreach (var take in new[] { "a", "b" }.Take(sound.Folder == "ambience" ? 1 : 2))
                {
                    var text = $"{style} Sound: {sound.Prompt}. Duration about {sound.Seconds.ToString("0.0", CultureInfo.InvariantCulture)} seconds.";
                    var name = sound.Folder == "ambience"
                        ? $"ambience-{sound.Name}.wav"
                        : $"{sound.Folder}-{sound.Name}.{take}.wav";

                    var body = new JsonObject
                    {
                        ["text"] = text,
                        ["model_id"] = SoundModel,
                        ["duration_seconds"] = sound.Seconds,
                        ["loop"] = sound.Loop,
                    };

                    var outcome = await SendAsync(
                        $"{BaseUrl}/sound-generation?output_format=wav_{SoundRate}", "sound-generation", SoundModel,
                        $"wav_{SoundRate}", body, name, token);

                    Report.AppendLine(CultureInfo.InvariantCulture,
                        $"| {FileText(outcome, name)} | {text.Replace('|', '/')} | {sound.Seconds.ToString("0.0", CultureInfo.InvariantCulture)} | {sound.Loop} | {StatusText(outcome)} | {Seconds(outcome)} | {outcome.Credits?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} |");
                }
            }

            Report.AppendLine();
        }

        private async Task EffectsAsync(CancellationToken token)
        {
            Report.AppendLine("## Part 2. Voice effects");
            Report.AppendLine();

            var catalog = await CatalogAsync(token);

            Report.AppendLine("### Catalog, `GET /v1/audio-effects/catalog`");
            Report.AppendLine();
            Report.AppendLine("```json");
            Report.AppendLine(catalog.Raw);
            Report.AppendLine("```");
            Report.AppendLine();

            var beds = catalog.Noise
                .ToList();

            var requests = new List<(string Label, string Name, JsonObject? Effects, bool Stream, string Format, bool Radio)>
            {
                ("baseline, no audio_effects", "baseline.wav", null, false, $"pcm_{SpeechRate}", false),
                ("distance 0.2 (near)", "distance-near.wav", new JsonObject { ["distance"] = 0.2 }, false, $"pcm_{SpeechRate}", false),
                ("distance 0.9 (far)", "distance-far.wav", new JsonObject { ["distance"] = 0.9 }, false, $"pcm_{SpeechRate}", true),
            };

            foreach (var id in beds)
            {
                requests.Add(($"background_noise_id {id}", $"noise-{id}.wav", new JsonObject { ["background_noise_id"] = id }, false, $"pcm_{SpeechRate}", true));
            }

            if (catalog.Filters.Count > 0)
            {
                var filter = catalog.Filters.FirstOrDefault(f => f.Contains("radio", StringComparison.OrdinalIgnoreCase)) ?? catalog.Filters[0];
                requests.Add(($"filter_preset_id {filter}", $"filter-{filter}.wav", new JsonObject { ["filter_preset_id"] = filter }, false, $"pcm_{SpeechRate}", false));
            }

            Report.AppendLine("### Plain endpoint, pcm_24000");
            Report.AppendLine();
            Report.AppendLine("| request | status | seconds | channels | credits | file |");
            Report.AppendLine("| --- | --- | --- | --- | --- | --- |");

            var radioInputs = new List<(string Name, Outcome Outcome)>();

            foreach (var (label, name, effects, stream, format, radio) in requests)
            {
                var outcome = await SpeakAsync(name, effects, stream, format, token);
                Row(label, outcome, name);

                if (radio && outcome.Status == 200)
                {
                    radioInputs.Add((Path.GetFileNameWithoutExtension(name), outcome));
                }
            }

            Report.AppendLine();
            Report.AppendLine("### After `RadioVoice.Apply`");
            Report.AppendLine();
            Report.AppendLine("| input | strength | file |");
            Report.AppendLine("| --- | --- | --- |");

            foreach (var (name, outcome) in radioInputs)
            {
                foreach (var strength in new[] { 1.0 }.Concat(name == radioInputs[0].Name ? [0.5] : Array.Empty<double>()))
                {
                    var clip = new AudioClip(name, outcome.Audio, new AudioFormat(SpeechRate, 1));
                    var radioed = RadioVoice.Apply(clip, strength);
                    var file = $"{name}.radio-{strength.ToString("0.0", CultureInfo.InvariantCulture)}.wav";
                    await File.WriteAllBytesAsync(Path.Combine(folder, file), Wav(radioed.Pcm.ToArray(), SpeechRate), token);
                    Report.AppendLine(CultureInfo.InvariantCulture, $"| `{name}` | {strength:0.0} | `{file}` |");
                }
            }

            Report.AppendLine();
            Report.AppendLine("### Panning and reverb: which formats return stereo");
            Report.AppendLine();
            Report.AppendLine("| request | status | seconds | channels | credits | file |");
            Report.AppendLine("| --- | --- | --- | --- | --- | --- |");

            var left = new JsonObject { ["pan"] = -1.0 };
            var centre = new JsonObject { ["pan"] = 0.0 };

            foreach (var (label, name, effects, format) in new (string, string, JsonObject, string)[]
            {
                ("pan -1 (hard left), pcm_24000", "pan-left.pcm.wav", left, $"pcm_{SpeechRate}"),
                ("pan 0 (centre), pcm_24000", "pan-centre.pcm.wav", centre, $"pcm_{SpeechRate}"),
                ("pan -1 (hard left), wav_24000", "pan-left.wav", left, $"wav_{SpeechRate}"),
                ("pan 0 (centre), wav_24000", "pan-centre.wav", centre, $"wav_{SpeechRate}"),
                ("pan -1 (hard left), mp3_44100_128", "pan-left.mp3", left, "mp3_44100_128"),
                ("environment hall (reverb), pcm_24000", "reverb.pcm.wav", new JsonObject { ["environment_id"] = "hall" }, $"pcm_{SpeechRate}"),
                ("environment hall (reverb), wav_24000", "reverb.wav", new JsonObject { ["environment_id"] = "hall" }, $"wav_{SpeechRate}"),
            })
            {
                Row(label, await SpeakAsync(name, Clone(effects), stream: false, format, token), name);
            }

            Report.AppendLine();
            Report.AppendLine("### `/stream`, one request per effect, pcm_24000");
            Report.AppendLine();
            Report.AppendLine("| request | status | seconds | channels | credits | file |");
            Report.AppendLine("| --- | --- | --- | --- | --- | --- |");

            var streamed = new List<(string Label, string Name, JsonObject Effects)>
            {
                ("distance 0.9", "stream-distance.wav", new JsonObject { ["distance"] = 0.9 }),
            };

            if (beds.Count > 0)
            {
                streamed.Add(($"background_noise_id {beds[0]}", "stream-noise.wav", new JsonObject { ["background_noise_id"] = beds[0] }));
            }

            if (catalog.Filters.Count > 0)
            {
                streamed.Add(($"filter_preset_id {catalog.Filters[0]}", "stream-filter.wav", new JsonObject { ["filter_preset_id"] = catalog.Filters[0] }));
            }

            streamed.Add(("pan -1", "stream-pan.wav", new JsonObject { ["pan"] = -1.0 }));

            if (catalog.Environments.Count > 0)
            {
                streamed.Add(($"environment_id {catalog.Environments[0]}", "stream-environment.wav", new JsonObject { ["environment_id"] = catalog.Environments[0] }));
            }

            foreach (var (label, name, effects) in streamed)
            {
                Row(label, await SpeakAsync(name, effects, stream: true, $"pcm_{SpeechRate}", token), name);
            }

            Report.AppendLine();
        }

        private void Row(string label, Outcome outcome, string name) =>
            Report.AppendLine(CultureInfo.InvariantCulture,
                $"| {label} | {StatusText(outcome)} | {Seconds(outcome)} | {(outcome.Audio.Length == 0 ? "—" : outcome.Channels.ToString(CultureInfo.InvariantCulture))} | {outcome.Credits?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {FileText(outcome, name)} |");

        private static JsonObject Clone(JsonObject value) => (JsonObject)value.DeepClone();

        private async Task<Outcome> SpeakAsync(string name, JsonObject? effects, bool stream, string format, CancellationToken token)
        {
            var body = new JsonObject
            {
                ["text"] = SpeechText,
                ["model_id"] = ElevenLabsModels.V4Turbo,
                ["language_code"] = ElevenLabsTtsProvider.Language,
            };

            if (effects is not null)
            {
                body["audio_effects"] = effects;
            }

            var endpoint = $"text-to-speech/{{voice}}{(stream ? "/stream" : "")}";
            var url = $"{BaseUrl}/text-to-speech/{Uri.EscapeDataString(voice)}{(stream ? "/stream" : "")}?output_format={format}";
            return await SendAsync(url, endpoint, ElevenLabsModels.V4Turbo, format, body, name, token);
        }

        private async Task<Outcome> SendAsync(
            string url, string endpoint, string model, string format, JsonObject body, string name, CancellationToken token)
        {
            var before = await CharacterCountAsync(token);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("xi-api-key", key);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

                using var response = await http.SendAsync(request, token);
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                await Task.Delay(2000, token);
                var after = await CharacterCountAsync(token);
                long? credits = before is { } b && after is { } a ? a - b : null;
                var status = (int)response.StatusCode;
                var bodyText = body.ToJsonString();

                if (!response.IsSuccessStatusCode)
                {
                    return new Outcome(endpoint, model, format, bodyText, status, DetailOf(bytes) ?? Encoding.UTF8.GetString(bytes), [], 0, 0, credits);
                }

                var (pcm, rate, channels) = Decode(bytes, format);
                var path = Path.Combine(folder, name);

                if (format.StartsWith("pcm_", StringComparison.Ordinal))
                {
                    await File.WriteAllBytesAsync(path, Wav(pcm, rate, channels), token);
                }
                else
                {
                    await File.WriteAllBytesAsync(path, bytes, token);
                }

                return new Outcome(endpoint, model, format, bodyText, status, null, pcm, rate, channels, credits);
            }
            catch (HttpRequestException failure)
            {
                return new Outcome(endpoint, model, format, body.ToJsonString(), 0, failure.Message, [], 0, 0, null);
            }
        }

        private static (byte[] Pcm, int Rate, int Channels) Decode(byte[] bytes, string format)
        {
            var rate = int.Parse(format.Split('_')[1], CultureInfo.InvariantCulture);

            if (format.StartsWith("pcm_", StringComparison.Ordinal))
            {
                return (bytes, rate, 1);
            }

            if (format.StartsWith("wav_", StringComparison.Ordinal) && bytes.Length > 44
                && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF")
            {
                var channels = BitConverter.ToInt16(bytes, 22);
                var wavRate = BitConverter.ToInt32(bytes, 24);
                var data = IndexOf(bytes, "data"u8);
                var offset = data < 0 ? 44 : data + 8;
                return (bytes[offset..], wavRate, channels);
            }

            return (bytes, rate, 0);
        }

        private static int IndexOf(byte[] bytes, ReadOnlySpan<byte> marker) =>
            bytes.AsSpan().IndexOf(marker);

        private sealed record Catalog(string Raw, List<string> Filters, List<string> Environments, List<string> Noise);

        private async Task<Catalog> CatalogAsync(CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/audio-effects/catalog");
            request.Headers.Add("xi-api-key", key);
            using var response = await http.SendAsync(request, token);
            var text = await response.Content.ReadAsStringAsync(token);

            List<string> filters = [], environments = [], noise = [];

            try
            {
                using var document = JsonDocument.Parse(text);
                filters = IdsOf(document.RootElement, "filter");
                environments = IdsOf(document.RootElement, "environment");
                noise = IdsOf(document.RootElement, "noise");
            }
            catch (JsonException)
            {
            }

            return new Catalog(
                $"{(int)response.StatusCode}\n{(text.Length > 6000 ? text[..6000] + " ..." : text)}",
                filters, environments, noise);
        }

        private static List<string> IdsOf(JsonElement root, string group)
        {
            var ids = new List<string>();

            foreach (var property in root.EnumerateObject())
            {
                if (!property.Name.Contains(group, StringComparison.OrdinalIgnoreCase)
                    || property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        ids.Add(item.GetString()!);
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        var id = item.EnumerateObject().FirstOrDefault(
                            field => field.Name.EndsWith("id", StringComparison.Ordinal) && field.Value.ValueKind == JsonValueKind.String);

                        if (id.Name is not null)
                        {
                            ids.Add(id.Value.GetString()!);
                        }
                    }
                }
            }

            return ids;
        }

        private async Task<long?> CharacterCountAsync(CancellationToken token)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (await ReadCharacterCountAsync(token) is { } count)
                {
                    return count;
                }

                await Task.Delay(1500, token);
            }

            return null;
        }

        private async Task<long?> ReadCharacterCountAsync(CancellationToken token)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/user/subscription");
                request.Headers.Add("xi-api-key", key);
                using var response = await http.SendAsync(request, token);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                return document.RootElement.TryGetProperty("character_count", out var count)
                    && count.TryGetInt64(out var value)
                        ? value
                        : null;
            }
            catch (Exception failure) when (failure is HttpRequestException or JsonException)
            {
                return null;
            }
        }

        private static string Delta(long? before, long? after) =>
            before is { } b && after is { } a ? (a - b).ToString(CultureInfo.InvariantCulture) : "n/a";

        private static string StatusText(Outcome outcome) =>
            outcome.Detail is { Length: > 0 } detail
                ? $"{outcome.Status}: {detail.Replace('|', '/').Replace('\n', ' ')}"
                : outcome.Status.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(Outcome outcome) =>
            outcome.Audio.Length == 0 || outcome.Channels == 0
                ? "—"
                : (outcome.Audio.Length / (outcome.SampleRate * 2.0 * outcome.Channels)).ToString("0.00", CultureInfo.InvariantCulture);

        private static string FileText(Outcome outcome, string name) =>
            outcome.Audio.Length == 0 ? "—" : $"`{name}`";

        private static string? AssetsFolder()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
                {
                    return Path.Combine(directory.FullName, "assets");
                }
            }

            return null;
        }
    }
}
