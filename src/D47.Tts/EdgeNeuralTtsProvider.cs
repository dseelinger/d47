using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>The free voices, spoken by the same service Edge's Read Aloud uses.</summary>
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

    /// <summary>What leaves the machine for this provider.</summary>
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
            // An empty list is a supported answer: the picker still lets the Commander keep the current value
            // or type one (Phase 4).
            logger.LogWarning(ex, "Could not list Edge Neural voices");
            return VoiceCatalogue.Unreachable(ex.Message);
        }
    }

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        if (voice.VoiceId is not { Length: > 0 } voiceId)
        {
            throw new TtsException("No Edge Neural voice has been chosen. Pick one in Settings.");
        }

        try
        {
            using var socket = await OpenAsync(text, voiceId, voice.Rate, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// The same request as <see cref="SynthesizeAsync"/>; returns once the first audio frame has been decoded
    /// and appended, and keeps appending as frames arrive.
    /// </summary>
    public async Task<ArrivingClip> StreamAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        if (voice.VoiceId is not { Length: > 0 } voiceId)
        {
            throw new TtsException("No Edge Neural voice has been chosen. Pick one in Settings.");
        }

        ClientWebSocket? socket = null;
        var reading = false;

        try
        {
            socket = await OpenAsync(text, voiceId, voice.Rate, cancellationToken).ConfigureAwait(false);

            var frames = ReceiveFramesAsync(socket, cancellationToken).GetAsyncEnumerator(cancellationToken);
            var arriving = new ArrivingClip(text);
            var decoder = new Mp3StreamDecoder(EdgeProtocol.SourceSampleRate, 1);
            var upsample = new PcmUpsampler();

            try
            {
                if (!await frames.MoveNextAsync().ConfigureAwait(false))
                {
                    throw new TtsException($"Edge Neural returned no audio for \"{Excerpt(text)}\".");
                }

                arriving.Append(upsample.Push(decoder.Push(frames.Current)));
            }
            catch
            {
                decoder.Dispose();
                await frames.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            // Owns the socket from here.
            reading = true;
            _ = AppendRestAsync(socket, frames, decoder, upsample, arriving, text);
            return arriving;
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
        finally
        {
            if (!reading)
            {
                socket?.Dispose();
            }
        }
    }

    private async Task AppendRestAsync(
        ClientWebSocket socket,
        IAsyncEnumerator<byte[]> frames,
        Mp3StreamDecoder decoder,
        PcmUpsampler upsample,
        ArrivingClip arriving,
        string text)
    {
        try
        {
            while (await frames.MoveNextAsync().ConfigureAwait(false))
            {
                arriving.Append(upsample.Push(decoder.Push(frames.Current)));
            }

            arriving.Append(upsample.Finish());
            arriving.Complete();
        }
        catch (OperationCanceledException cancelled)
        {
            arriving.Fail(cancelled);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Edge Neural stopped sending \"{Text}\" part-way", Excerpt(text));

            arriving.Fail(ex as TtsException
                          ?? new TtsException($"Edge Neural could not finish \"{Excerpt(text)}\": {ex.Message}", ex));
        }
        finally
        {
            decoder.Dispose();
            await frames.DisposeAsync().ConfigureAwait(false);
            socket.Dispose();
        }
    }

    /// <summary>A connected socket with the configuration and the SSML already sent.</summary>
    private static async Task<ClientWebSocket> OpenAsync(
        string text,
        string voiceId,
        double rate,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var socket = new ClientWebSocket();

        try
        {
            socket.Options.SetRequestHeader("Pragma", "no-cache");
            socket.Options.SetRequestHeader("Cache-Control", "no-cache");
            socket.Options.SetRequestHeader("Origin", EdgeProtocol.Origin);
            socket.Options.SetRequestHeader("User-Agent", EdgeProtocol.UserAgent);
            socket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            socket.Options.SetRequestHeader("Cookie", EdgeProtocol.MuidCookie());

            var url = $"{SynthesisUrl}?TrustedClientToken={EdgeProtocol.TrustedClientToken}" +
                      $"&{EdgeProtocol.SecurityQuery(now)}&ConnectionId={Guid.NewGuid():N}";

            await socket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

            await SendAsync(socket, EdgeProtocol.Configuration(now), cancellationToken).ConfigureAwait(false);
            await SendAsync(
                socket,
                EdgeProtocol.SsmlRequest(text, voiceId, rate, Guid.NewGuid().ToString("N"), now),
                cancellationToken).ConfigureAwait(false);

            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static Task SendAsync(ClientWebSocket socket, string message, CancellationToken cancellationToken) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

    /// <summary>Reads until the service says the turn ended.</summary>
    private static async Task<byte[]> ReceiveAudioAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var audio = new MemoryStream();

        await foreach (var chunk in ReceiveFramesAsync(socket, cancellationToken).ConfigureAwait(false))
        {
            audio.Write(chunk);
        }

        return audio.ToArray();
    }

    /// <summary>The audio of each binary frame, ending at the turn end or a close.</summary>
    private static async IAsyncEnumerable<byte[]> ReceiveFramesAsync(
        ClientWebSocket socket,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
                    yield break;
                }

                frame.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var bytes = frame.GetBuffer().AsSpan(0, (int)frame.Length);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                if (Encoding.UTF8.GetString(bytes).Contains("Path:turn.end", StringComparison.Ordinal))
                {
                    yield break;
                }

                continue;
            }

            var audio = EdgeProtocol.AudioOf(bytes);

            if (!audio.IsEmpty)
            {
                yield return audio.ToArray();
            }
        }
    }

    /// <summary>"Microsoft Sonia Online (Natural) - English (United Kingdom)" is not a label.</summary>
    private static string Shorten(string friendlyName)
    {
        var parts = friendlyName.Split(' ');
        return parts.Length >= 2 && parts[0] == "Microsoft" ? parts[1] : friendlyName;
    }

    /// <summary>MP3 to 16-bit PCM through the OS codec.</summary>
    internal static byte[] Decode(byte[] mp3)
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
