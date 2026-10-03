using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// A one-off live measurement of what <c>eleven_v4_turbo</c> accepts, written to a report for the maintainer to
/// read. Spends real characters, so it runs only with <c>D47_ELEVENLABS_PROBE=1</c> as well as the key.
/// </summary>
public class ElevenLabsV4TurboProbeTests
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";
    private const int SampleRate = 24000;

    private const string SpeedText =
        "Frame shift drive charged. The jump to Achenar is plotted and the route is clear of traffic. "
        + "Fuel holds for four more jumps, so scoop at the next main sequence star, Commander.";

    private const string FirstGroup =
        "Docking request granted, Commander. You are cleared for landing pad twelve. "
        + "Approach from the mail slot at no more than one hundred metres a second, and mind the traffic leaving.";

    private const string SecondGroup =
        "Landing gear is down and the pad is lit. Hold your position over the twelve, "
        + "match the station's rotation, and set her down gently. Welcome back to Jameson Memorial.";

    private const string ShortSentence = "Shields are down, Commander. Boost and get clear of the fight.";

    private const string LongGroup =
        "Hull integrity at forty percent and falling. Shields are down and the power plant is running hot. "
        + "Two hostile Vipers are closing from behind and a third is moving to cut off the escape vector. "
        + "Recommend boosting to the jump point now, Commander, and charging the frame shift drive on the way out.";

    private static string? Key => Environment.GetEnvironmentVariable("D47_ELEVENLABS_KEY");

    [Fact]
    public async Task TheV4TurboProbeWritesItsReport()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key)
                || Environment.GetEnvironmentVariable("D47_ELEVENLABS_PROBE") != "1",
            "set D47_ELEVENLABS_KEY and D47_ELEVENLABS_PROBE=1 to run the paid v4 Turbo probe");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
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

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var probe = new Probe(http, Key!, voice, folder);

        await probe.RunAsync(token);

        var report = Path.Combine(folder, "report.md");
        await File.WriteAllTextAsync(report, probe.Report.ToString(), token);

        TestContext.Current.TestOutputHelper?.WriteLine($"ElevenLabs v4 Turbo probe written to {folder}");
        Assert.True(File.Exists(report));
    }

    private sealed record Outcome(int Status, string? Detail, byte[] Pcm, string? RequestId);

    private sealed class Probe(HttpClient http, string key, string voice, string folder)
    {
        public StringBuilder Report { get; } = new();

        private readonly StringBuilder _headers = new();

        public async Task RunAsync(CancellationToken token)
        {
            Report.AppendLine("# ElevenLabs v4 Turbo probe");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture, $"- Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Report.AppendLine(CultureInfo.InvariantCulture, $"- Model: `{ElevenLabsModels.V4Turbo}`");
            Report.AppendLine(CultureInfo.InvariantCulture, $"- Voice: `{voice}`");
            Report.AppendLine("- Output: `pcm_24000`, written as 24 kHz mono 16-bit WAV without resampling");
            Report.AppendLine();

            await SpeedAsync(token);
            await StitchingAsync(token);
            await IpaAsync(token);
            await LatencyAsync(token);
            await SoundEffectsAsync(token);

            Report.AppendLine("## 4. Response headers, every request");
            Report.AppendLine();
            Report.Append(_headers);
        }

        private async Task SpeedAsync(CancellationToken token)
        {
            Report.AppendLine("## 1. Speed");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture, $"Text, {SpeedText.Length} characters: \"{SpeedText}\"");
            Report.AppendLine();
            Report.AppendLine("| speed | status | duration (s) | file |");
            Report.AppendLine("| --- | --- | --- | --- |");

            foreach (var speed in new[] { 0.7, 1.0, 1.2 })
            {
                var name = $"speed-{(int)Math.Round(speed * 100):000}.wav";
                var body = Body(SpeedText);
                body["voice_settings"] = new JsonObject { ["speed"] = speed };

                var outcome = await SynthesizeAsync(name, body, token);
                Row(speed.ToString("0.0", CultureInfo.InvariantCulture), outcome, name);
            }

            Report.AppendLine();
        }

        private async Task StitchingAsync(CancellationToken token)
        {
            Report.AppendLine("## 2–3. Stitching: `previous_text` and `previous_request_ids`");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture, $"First group, {FirstGroup.Length} characters: \"{FirstGroup}\"");
            Report.AppendLine();
            Report.AppendLine(CultureInfo.InvariantCulture, $"Second group, {SecondGroup.Length} characters: \"{SecondGroup}\"");
            Report.AppendLine();
            Report.AppendLine("`character_count` is read from `GET /v1/user/subscription` before and after each request. "
                + "The delta beside the second group's length shows whether the context was billed; "
                + "the counter may update late, so a delta of 0 is not conclusive on its own.");
            Report.AppendLine();
            Report.AppendLine("| request | status | duration (s) | chars before | chars after | delta | file |");
            Report.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            var first = await BilledAsync("first group", "first-group.wav", Body(FirstGroup), token);

            await BilledAsync("second group, unstitched", "unstitched.wav", Body(SecondGroup), token);

            var withText = Body(SecondGroup);
            withText["previous_text"] = FirstGroup;
            await BilledAsync("second group, previous_text", "stitched-previous-text.wav", withText, token);

            if (first.RequestId is { Length: > 0 } requestId)
            {
                var withId = Body(SecondGroup);
                withId["previous_request_ids"] = new JsonArray(requestId);
                await BilledAsync("second group, previous_request_ids", "stitched-request-id.wav", withId, token);
            }

            Report.AppendLine();
            Report.AppendLine(first.RequestId is { Length: > 0 } id
                ? $"The first group's `request-id` header was `{id}`."
                : "The first group's response carried no `request-id` header, so `previous_request_ids` was not tried.");
            Report.AppendLine();
        }

        private async Task<Outcome> BilledAsync(string label, string name, JsonObject body, CancellationToken token)
        {
            var before = await CharacterCountAsync(token);
            var outcome = await SynthesizeAsync(name, body, token);
            var after = await CharacterCountAsync(token);

            var delta = before is { } b && after is { } a ? (a - b).ToString(CultureInfo.InvariantCulture) : "n/a";
            Report.AppendLine(CultureInfo.InvariantCulture,
                $"| {label} | {StatusText(outcome)} | {Seconds(outcome)} | {before?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {after?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {delta} | {FileText(outcome, name)} |");

            return outcome;
        }

        private async Task IpaAsync(CancellationToken token)
        {
            Report.AppendLine("## 5. Inline IPA");
            Report.AppendLine();
            Report.AppendLine("The three marked-up forms are guesses from the issue; no form published by ElevenLabs "
                + "was added. Listen to each against `ipa-plain.wav`.");
            Report.AppendLine();
            Report.AppendLine("| form | text | status | duration (s) | file |");
            Report.AppendLine("| --- | --- | --- | --- | --- |");

            var forms = new (string Name, string Text)[]
            {
                ("plain", "Achenar"),
                ("phoneme-tag", "<phoneme alphabet=\"ipa\" ph=\"ˈæk.ə.nɑː\">Achenar</phoneme>"),
                ("pronounced", "[pronounced ˈæk.ə.nɑː] Achenar"),
                ("slashes", "Achenar /ˈæk.ə.nɑː/"),
            };

            foreach (var (formName, text) in forms)
            {
                var name = $"ipa-{formName}.wav";
                var outcome = await SynthesizeAsync(name, Body(text), token);
                Report.AppendLine(CultureInfo.InvariantCulture,
                    $"| {formName} | `{text}` | {StatusText(outcome)} | {Seconds(outcome)} | {FileText(outcome, name)} |");
            }

            Report.AppendLine();
        }

        private async Task LatencyAsync(CancellationToken token)
        {
            Report.AppendLine("## 6. Latency");
            Report.AppendLine();
            Report.AppendLine("Three runs each, read with `HttpCompletionOption.ResponseHeadersRead`. "
                + "First byte is the first body byte read; last byte is the end of the body. Medians in milliseconds.");
            Report.AppendLine();
            Report.AppendLine("| text | endpoint | statuses | first byte (ms) | last byte (ms) |");
            Report.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var (label, text) in new[] { ($"short, {ShortSentence.Length} chars", ShortSentence), ($"group, {LongGroup.Length} chars", LongGroup) })
            {
                foreach (var stream in new[] { false, true })
                {
                    var firsts = new List<double>();
                    var lasts = new List<double>();
                    var statuses = new List<string>();

                    for (var run = 0; run < 3; run++)
                    {
                        var (status, first, last) = await TimeAsync(Body(text), stream, token);
                        statuses.Add(status.ToString(CultureInfo.InvariantCulture));

                        if (status == 200 && first is { } f && last is { } l)
                        {
                            firsts.Add(f);
                            lasts.Add(l);
                        }
                    }

                    Report.AppendLine(CultureInfo.InvariantCulture,
                        $"| {label} | {(stream ? "`/stream`" : "non-streaming")} | {string.Join(", ", statuses)} | {Median(firsts)} | {Median(lasts)} |");
                }
            }

            Report.AppendLine();
        }

        private async Task SoundEffectsAsync(CancellationToken token)
        {
            Report.AppendLine("## 7. Sound effects");
            Report.AppendLine();
            Report.AppendLine("| text | status | duration (s) | file |");
            Report.AppendLine("| --- | --- | --- | --- |");

            var lines = new (string Name, string Text)[]
            {
                ("klaxon", "[klaxon blaring] Hull integrity at forty percent."),
                ("shield-collapse", "[shield collapse alarm] Shields are down."),
                ("proximity", "[proximity warning beeping] Contact closing fast."),
            };

            foreach (var (lineName, text) in lines)
            {
                var name = $"sfx-{lineName}.wav";
                var outcome = await SynthesizeAsync(name, Body(text), token);
                Report.AppendLine(CultureInfo.InvariantCulture,
                    $"| `{text}` | {StatusText(outcome)} | {Seconds(outcome)} | {FileText(outcome, name)} |");
            }

            Report.AppendLine();
        }

        private void Row(string label, Outcome outcome, string name) =>
            Report.AppendLine(CultureInfo.InvariantCulture,
                $"| {label} | {StatusText(outcome)} | {Seconds(outcome)} | {FileText(outcome, name)} |");

        private static JsonObject Body(string text) => new()
        {
            ["text"] = text,
            ["model_id"] = ElevenLabsModels.V4Turbo,
            ["language_code"] = ElevenLabsTtsProvider.Language,
        };

        private HttpRequestMessage Request(HttpMethod method, string url, JsonObject? body = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("xi-api-key", key);

            if (body is not null)
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            return request;
        }

        private string SpeechUrl(bool stream) =>
            $"{BaseUrl}/text-to-speech/{Uri.EscapeDataString(voice)}{(stream ? "/stream" : "")}?output_format=pcm_{SampleRate}";

        private async Task<Outcome> SynthesizeAsync(string name, JsonObject body, CancellationToken token)
        {
            try
            {
                using var request = Request(HttpMethod.Post, SpeechUrl(stream: false), body);
                using var response = await http.SendAsync(request, token);
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                var headers = HeadersOf(response);
                var requestId = response.Headers.TryGetValues("request-id", out var ids) ? ids.FirstOrDefault() : null;

                _headers.AppendLine(CultureInfo.InvariantCulture, $"### {name} — {(int)response.StatusCode}");
                _headers.AppendLine();
                _headers.AppendLine("```");
                _headers.Append(headers);
                _headers.AppendLine("```");
                _headers.AppendLine();

                if (!response.IsSuccessStatusCode)
                {
                    return new Outcome((int)response.StatusCode, DetailOf(bytes), [], requestId);
                }

                await File.WriteAllBytesAsync(Path.Combine(folder, name), Wav(bytes), token);
                return new Outcome((int)response.StatusCode, null, bytes, requestId);
            }
            catch (HttpRequestException failure)
            {
                return new Outcome(0, failure.Message, [], null);
            }
        }

        private async Task<(int Status, double? First, double? Last)> TimeAsync(
            JsonObject body, bool stream, CancellationToken token)
        {
            try
            {
                using var request = Request(HttpMethod.Post, SpeechUrl(stream), body);
                var clock = Stopwatch.StartNew();
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (!response.IsSuccessStatusCode)
                {
                    return ((int)response.StatusCode, null, null);
                }

                await using var content = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[8192];
                double? first = null;

                int read;
                while ((read = await content.ReadAsync(buffer, token)) > 0)
                {
                    first ??= clock.Elapsed.TotalMilliseconds;
                }

                return ((int)response.StatusCode, first, clock.Elapsed.TotalMilliseconds);
            }
            catch (HttpRequestException)
            {
                return (0, null, null);
            }
        }

        private async Task<long?> CharacterCountAsync(CancellationToken token)
        {
            try
            {
                using var request = Request(HttpMethod.Get, $"{BaseUrl}/user/subscription");
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

        private static string HeadersOf(HttpResponseMessage response)
        {
            var text = new StringBuilder();

            foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"{name}: {string.Join(", ", values)}");
            }

            return text.ToString();
        }

        /// <summary>ElevenLabs' <c>detail.message</c>, or each <c>msg</c> of a validation error list.</summary>
        private static string? DetailOf(byte[] body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);

                if (!document.RootElement.TryGetProperty("detail", out var detail))
                {
                    return null;
                }

                return detail.ValueKind switch
                {
                    JsonValueKind.Object when detail.TryGetProperty("message", out var message) => message.GetString(),
                    JsonValueKind.String => detail.GetString(),
                    JsonValueKind.Array => string.Join("; ", detail.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("msg", out _))
                        .Select(item => item.GetProperty("msg").GetString())),
                    _ => null,
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static byte[] Wav(byte[] pcm)
        {
            using var stream = new MemoryStream(44 + pcm.Length);
            using var writer = new BinaryWriter(stream);

            writer.Write("RIFF"u8);
            writer.Write(36 + pcm.Length);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
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

            return stream.ToArray();
        }

        private static string StatusText(Outcome outcome) =>
            outcome.Detail is { Length: > 0 } detail
                ? $"{outcome.Status}: {detail.Replace('|', '/')}"
                : outcome.Status.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(Outcome outcome) =>
            outcome.Pcm.Length == 0
                ? "—"
                : (outcome.Pcm.Length / (SampleRate * 2.0)).ToString("0.00", CultureInfo.InvariantCulture);

        private static string FileText(Outcome outcome, string name) =>
            outcome.Pcm.Length == 0 ? "—" : $"`{name}`";

        private static string Median(List<double> values)
        {
            if (values.Count == 0)
            {
                return "—";
            }

            values.Sort();
            var middle = values.Count / 2;
            var median = values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
            return median.ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
