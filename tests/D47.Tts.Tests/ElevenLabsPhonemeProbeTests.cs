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
/// A live probe of whether ElevenLabs models obey a phoneme tag or a respelling on words forced to a
/// pronunciation. Spends real characters, so it runs only with <c>D47_ELEVENLABS_PROBE=1</c> as well as the key.
/// </summary>
public class ElevenLabsPhonemeProbeTests
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";
    private const int SampleRate = 24000;
    private const string Sentence = "Plotting the route to {0} now, Commander. Four jumps, and every star on the way can be scooped.";

    private sealed record Word(string Name, string Plain, string Other, string OtherForm);

    private static readonly Word[] Words =
    [
        new("Achenar", "Achenar", "<phoneme alphabet=\"ipa\" ph=\"ˈɑːkɪnɑːr\">Achenar</phoneme>", "tagged"),
        new("Jameson", "Jameson", "<phoneme alphabet=\"ipa\" ph=\"ˈjɑːmɛsɒn\">Jameson</phoneme>", "tagged"),
        new("Shinrarta-Dezhra", "Shinrarta Dezhra", "shin rar tah dezh rah", "respelled"),
        new("Diaguandri", "Diaguandri", "dee ah gwan dree", "respelled"),
    ];

    private static string? Key => Environment.GetEnvironmentVariable("D47_ELEVENLABS_KEY");

    [Fact]
    public async Task ThePhonemeProbeWritesItsReport()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key)
                || Environment.GetEnvironmentVariable("D47_ELEVENLABS_PROBE") != "1",
            "set D47_ELEVENLABS_KEY and D47_ELEVENLABS_PROBE=1 to run the paid phoneme probe");

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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp",
            "d47-elevenlabs-phoneme-probe",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        var report = new StringBuilder();
        report.AppendLine("# ElevenLabs phoneme probe");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"- Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Voice: `{voice}`");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Sentence: \"{Sentence}\"");
        report.AppendLine("- Output: `pcm_24000`, written as 24 kHz mono 16-bit WAV without resampling");
        report.AppendLine();

        foreach (var model in ElevenLabsModels.All)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"## `{model.Id}`");
            report.AppendLine();
            report.AppendLine("| word | form | status | duration (s) | character-cost |");
            report.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var word in Words)
            {
                foreach (var (form, text) in new[] { ("plain", word.Plain), (word.OtherForm, word.Other) })
                {
                    var name = $"{model.Id}-{word.Name}-{form}.wav";
                    var result = await SynthesizeAsync(http, voice, model.Id, string.Format(CultureInfo.InvariantCulture, Sentence, text), token);

                    if (result.Pcm.Length > 0)
                    {
                        await File.WriteAllBytesAsync(Path.Combine(folder, name), Wav(result.Pcm), token);
                    }

                    var status = result.Detail is { Length: > 0 } detail
                        ? $"{result.Status}: {detail.Replace('|', '/')}"
                        : result.Status.ToString(CultureInfo.InvariantCulture);
                    var seconds = result.Pcm.Length == 0
                        ? "—"
                        : (result.Pcm.Length / (SampleRate * 2.0)).ToString("0.00", CultureInfo.InvariantCulture);

                    report.AppendLine(CultureInfo.InvariantCulture,
                        $"| {word.Plain} | {form} | {status} | {seconds} | {result.Cost ?? "—"} |");
                }
            }

            report.AppendLine();
        }

        var path = Path.Combine(folder, "report.md");
        await File.WriteAllTextAsync(path, report.ToString(), token);

        TestContext.Current.TestOutputHelper?.WriteLine($"ElevenLabs phoneme probe written to {folder}");
        Assert.True(File.Exists(path));
    }

    private sealed record Outcome(int Status, string? Detail, byte[] Pcm, string? Cost);

    private static async Task<Outcome> SynthesizeAsync(
        HttpClient http, string voice, string model, string text, CancellationToken token)
    {
        var body = new JsonObject
        {
            ["text"] = text,
            ["model_id"] = model,
            ["language_code"] = ElevenLabsTtsProvider.Language,
        };

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/text-to-speech/{Uri.EscapeDataString(voice)}?output_format=pcm_{SampleRate}");
            request.Headers.Add("xi-api-key", Key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            var cost = response.Headers.TryGetValues("character-cost", out var values) ? values.FirstOrDefault() : null;

            return response.IsSuccessStatusCode
                ? new Outcome((int)response.StatusCode, null, bytes, cost)
                : new Outcome((int)response.StatusCode, DetailOf(bytes), [], cost);
        }
        catch (HttpRequestException failure)
        {
            return new Outcome(0, failure.Message, [], null);
        }
        catch (TaskCanceledException failure) when (!token.IsCancellationRequested)
        {
            return new Outcome(0, failure.Message, [], null);
        }
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
}
