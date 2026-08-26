using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// The free voices, spoken by the same service Edge's Read Aloud uses.
/// <para>
/// This is an undocumented endpoint. It is named in architecture.md §2 as the free provider
/// and it sounds far better than anything local, but it is not a supported API and it can
/// change without notice — which is exactly why it sits behind <see cref="ITtsProvider"/> and
/// why a failure here degrades to "the speech capability is off" rather than to a crash
/// (list.md Phase 3, "Capabilities as state, not guard").
/// </para>
/// <para>
/// <b>This is the one provider that decodes.</b> Raw PCM was preferred here for as long as it
/// was offered — no decoder in the graph, which is what keeps FFmpeg and its copyleft problem
/// out of the dependency set (architecture.md §10) — and the endpoint withdrew the raw formats
/// in mid-2026. So audio arrives as 24 kHz mono MP3 and is decoded through NAudio, which is
/// already in the graph and is MIT. <see cref="EdgeProtocol.SourceSampleRate"/> carries the
/// history and the exact refusal; it is not repeated here.
/// </para>
/// <para>
/// Stated as an exception rather than left implied, because the sentence that used to stand
/// here read as a rule the whole audio path keeps. It is not one: the preference for a format
/// nothing has to decode is real and every other provider still meets it, and this one no
/// longer can. A provider argued against on the grounds that it would put a decoder in the
/// graph is being held to a bar Edge itself does not clear — which is a different argument
/// from the licence one, and the licence one is the one that binds.
/// </para>
/// </summary>
public sealed class EdgeNeuralTtsProvider(ILogger<EdgeNeuralTtsProvider> logger, HttpClient? http = null)
    : ITtsProvider, IDisposable
{
    private const string VoiceListUrl =
        "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list";

    private const string SynthesisUrl =
        "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

    private readonly HttpClient _http = http ?? new HttpClient();
    private readonly bool _ownsHttp = http is null;

    private VoiceCatalogue? _voices;

    public string Id => "edge";

    public string Name => "Edge Neural";

    /// <summary>
    /// Used when nothing has been chosen. A British English voice, because the game's own
    /// fiction is, and because a default nobody picked should at least be defensible.
    /// </summary>
    public const string DefaultVoice = "en-GB-SoniaNeural";

    /// <summary>
    /// What leaves the machine for this provider. Stated once here so the settings row and
    /// the documentation page read it from one place rather than each asserting it separately
    /// (list.md Phase 4, "Say what each provider receives").
    /// </summary>
    public const string EgressDisclosure =
        "The text of every reply D47 speaks is sent to Microsoft's Edge Read Aloud service to be " +
        "turned into audio. No game state, no journal content and no keys are sent. Selecting no " +
        "voice provider keeps D47 silent and sends nothing.";

    public async Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (_voices is { } cached)
        {
            return cached;
        }

        try
        {
            var url = $"{VoiceListUrl}?trustedclienttoken={EdgeProtocol.TrustedClientToken}" +
                      $"&{EdgeProtocol.SecurityQuery(DateTimeOffset.UtcNow)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Origin", EdgeProtocol.Origin);
            request.Headers.Add("User-Agent", EdgeProtocol.UserAgent);
            request.Headers.Add("Cookie", EdgeProtocol.MuidCookie());
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var listed = await JsonSerializer
                .DeserializeAsync<List<EdgeVoice>>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _voices = VoiceCatalogue.Of(
            [
                .. (listed ?? [])
                    .Where(voice => voice.ShortName is not null)
                    .Select(voice => new VoiceInfo(
                        voice.ShortName!,
                        voice.FriendlyName is { } friendly ? Shorten(friendly) : voice.ShortName!,
                        voice.Locale ?? string.Empty,
                        voice.Gender))
                    .OrderBy(voice => voice.Locale, StringComparer.Ordinal)
                    .ThenBy(voice => voice.Name, StringComparer.Ordinal),
            ]);

            logger.LogInformation("Edge Neural offers {Count} voices", _voices.Count);
            return _voices;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An empty list is a supported answer: the picker still lets the Commander keep
            // the current value or type one (list.md Phase 4). It says which kind of empty it
            // is, though — Edge needs no key, so unreachable is the only one it can be.
            logger.LogWarning(ex, "Could not list Edge Neural voices");
            return VoiceCatalogue.Unreachable(ex.Message);
        }
    }

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        using var socket = new ClientWebSocket();

        socket.Options.SetRequestHeader("Pragma", "no-cache");
        socket.Options.SetRequestHeader("Cache-Control", "no-cache");
        socket.Options.SetRequestHeader("Origin", EdgeProtocol.Origin);
        socket.Options.SetRequestHeader("User-Agent", EdgeProtocol.UserAgent);
        socket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        socket.Options.SetRequestHeader("Cookie", EdgeProtocol.MuidCookie());

        var url = $"{SynthesisUrl}?TrustedClientToken={EdgeProtocol.TrustedClientToken}" +
                  $"&{EdgeProtocol.SecurityQuery(now)}&ConnectionId={Guid.NewGuid():N}";

        try
        {
            await socket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

            await SendAsync(socket, EdgeProtocol.Configuration(now), cancellationToken).ConfigureAwait(false);
            await SendAsync(
                socket,
                EdgeProtocol.SsmlRequest(
                    text,
                    voice.VoiceId ?? DefaultVoice,
                    voice.Rate,
                    Guid.NewGuid().ToString("N"),
                    now),
                cancellationToken).ConfigureAwait(false);

            var mp3 = await ReceiveAudioAsync(socket, cancellationToken).ConfigureAwait(false);

            if (mp3.Length == 0)
            {
                throw new TtsException($"Edge Neural returned no audio for \"{Excerpt(text)}\".");
            }

            return new AudioClip(text, EdgeProtocol.Upsample(Decode(mp3)), AudioFormat.Standard);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TtsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TtsException($"Edge Neural could not speak \"{Excerpt(text)}\": {ex.Message}", ex);
        }
    }

    private static Task SendAsync(ClientWebSocket socket, string message, CancellationToken cancellationToken) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

    /// <summary>
    /// Reads until the service says the turn ended. Binary frames carry the audio; text frames
    /// are status, of which only `turn.end` matters.
    /// </summary>
    private static async Task<byte[]> ReceiveAudioAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var audio = new MemoryStream();
        var buffer = new byte[16 * 1024];

        while (socket.State == WebSocketState.Open)
        {
            using var frame = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return audio.ToArray();
                }

                frame.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var bytes = frame.GetBuffer().AsSpan(0, (int)frame.Length);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                if (Encoding.UTF8.GetString(bytes).Contains("Path:turn.end", StringComparison.Ordinal))
                {
                    return audio.ToArray();
                }

                continue;
            }

            audio.Write(EdgeProtocol.AudioOf(bytes));
        }

        return audio.ToArray();
    }

    /// <summary>"Microsoft Sonia Online (Natural) - English (United Kingdom)" is not a label.</summary>
    private static string Shorten(string friendlyName)
    {
        var parts = friendlyName.Split(' ');
        return parts.Length >= 2 && parts[0] == "Microsoft" ? parts[1] : friendlyName;
    }

    /// <summary>
    /// MP3 to 16-bit PCM through the OS codec. Strict about what comes out: the request named
    /// 24 kHz mono, and audio in any other shape reaching the arbiter would play at the wrong
    /// pitch — an error worth raising, not resampling around.
    /// </summary>
    private static byte[] Decode(byte[] mp3)
    {
        using var reader = new NAudio.Wave.Mp3FileReader(new MemoryStream(mp3));

        if (reader.WaveFormat.SampleRate != EdgeProtocol.SourceSampleRate
            || reader.WaveFormat.Channels != 1)
        {
            throw new TtsException(
                $"Edge Neural sent {reader.WaveFormat.SampleRate} Hz / " +
                $"{reader.WaveFormat.Channels}ch audio where 24 kHz mono was requested.");
        }

        using var pcm = new MemoryStream();
        reader.CopyTo(pcm);
        return pcm.ToArray();
    }

    private static string Excerpt(string text) => text.Length <= 40 ? text : text[..40] + "…";

    private sealed record EdgeVoice
    {
        public string? ShortName { get; init; }

        public string? FriendlyName { get; init; }

        public string? Locale { get; init; }

        public string? Gender { get; init; }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
