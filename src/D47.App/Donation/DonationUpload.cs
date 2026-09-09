using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Diagnostics.Donation;
using Microsoft.Extensions.Logging;

namespace D47.App.Donation;

/// <summary>
/// One POST, to one address, carrying one donation — or, since #167, one request to delete every
/// donation an installation ever made (#175).
/// </summary>
public sealed class DonationUpload
{
    /// <summary>The path a donation is posted to, appended to the configured address.</summary>
    public const string Path = "donate";

    /// <summary>The path a withdrawal is posted to (#167).</summary>
    public const string ForgetPath = "forget";

    /// <summary>How long one donation may take.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

    private readonly HttpClient? _http;
    private readonly ILogger? _log;

    /// <summary><param name="http"> The client, or null to make one per send.</summary>
    /// <param name="http">The client, or null to make one per send.</param>
    public DonationUpload(HttpClient? http = null, ILogger? log = null)
    {
        _http = http;
        _log = log;
    }

    /// <summary>The client used when none was injected.</summary>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-donation");
        return client;
    }

    /// <summary>
    /// Whether an address is one this will post to. https and nothing else: the payload is a scrubbed
    /// journal, and a plaintext destination would put it on the wire for anybody on the path — which is
    /// a worse failure than not donating.
    /// </summary>
    public static bool IsUsable(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Where a request actually goes, for the disclosure and for the receipt.</summary>
    public static string Destination(string endpoint) =>
        Destination(endpoint, Path);

    /// <inheritdoc cref="Destination(string)"/>
    public static string Destination(string endpoint, string path) =>
        new Uri(new Uri(endpoint.TrimEnd('/') + "/"), path).ToString();

    /// <summary>Sends it.</summary>
    /// <param name="endpoint">The configured address.</param>
    /// <param name="envelope">What is written on the outside.</param>
    /// <param name="payload">The scrubbed payload, exactly as consented to.</param>
    public Task<DonationOutcome> SendAsync(
        string endpoint,
        DonationEnvelope envelope,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancel = default) =>
        SendAsync(endpoint, envelope, new ByteArrayContent(Compress(payload.Span)), cancel);

    /// <summary>The same send, for a payload that was never held whole (#181).</summary>
    /// <param name="compressed">The gzipped payload, positioned at its start.</param>
    /// <param name="sent">Told how far the body has got, or null to say nothing (#212).</param>
    public Task<DonationOutcome> SendAsync(
        string endpoint,
        DonationEnvelope envelope,
        Stream compressed,
        IProgress<long>? sent = null,
        CancellationToken cancel = default)
    {
        // Taken before the wrap, off the stream that knows: the metered one answers the same, and reading it
        // from the thing being measured is one fewer place for the two to disagree.
        var length = compressed.Length - compressed.Position;

        var content = new StreamContent(
            sent is null ? compressed : new MeteredStream(compressed, sent));

        // Said rather than left to be inferred.
        content.Headers.ContentLength = length;

        return SendAsync(endpoint, envelope, content, cancel);
    }

    private async Task<DonationOutcome> SendAsync(
        string endpoint,
        DonationEnvelope envelope,
        HttpContent payload,
        CancellationToken cancel)
    {
        using var body = payload;

        if (!IsUsable(endpoint))
        {
            return DonationOutcome.Refused(
                "The donation address is not an https address, so nothing was sent.");
        }

        if (!envelope.IsWellFormed())
        {
            // Refused here rather than by the endpoint.
            return DonationOutcome.Refused(
                "d47 could not assemble a complete donation envelope, so nothing was sent.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Destination(endpoint));

        foreach (var (name, value) in envelope.Headers())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        request.Content = body;
        body.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(Patience);

        // Made here rather than held, when nobody handed one in.
        var http = _http ?? CreateClient();

        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            var said = (await response.Content.ReadAsStringAsync(deadline.Token)).Trim();

            if (!response.IsSuccessStatusCode)
            {
                _log?.LogWarning(
                    "Donation refused with {Status}: {Said}", (int)response.StatusCode, said);

                return DonationOutcome.Refused(Explain(response.StatusCode, said));
            }

            return DonationOutcome.Stored(KeyIn(said) ?? envelope.PredictedKey());
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return DonationOutcome.Refused(
                "The donation took longer than ten minutes and was given up on. Nothing was "
                + "confirmed as stored.");
        }
        catch (OperationCanceledException)
        {
            return DonationOutcome.Refused("Stopped. Nothing was confirmed as stored.");
        }
        catch (HttpRequestException ex)
        {
            _log?.LogWarning(ex, "Donation could not reach {Endpoint}.", endpoint);
            return DonationOutcome.Refused(
                "The donation address could not be reached, so nothing was sent.");
        }
        finally
        {
            if (_http is null)
            {
                http.Dispose();
            }
        }
    }

    /// <summary>Asks the store to delete every donation made under one installation identifier (#167).</summary>
    public async Task<ErasureOutcome> ForgetAsync(
        string endpoint,
        string donor,
        CancellationToken cancel = default)
    {
        if (!IsUsable(endpoint))
        {
            return ErasureOutcome.NotAsked(
                "The donation address is not an https address, so no store was asked.");
        }

        if (!DonorToken.IsWellFormed(donor))
        {
            return ErasureOutcome.NotAsked(
                "There is no well-formed donation identifier here, so no store was asked.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post, Destination(endpoint, ForgetPath));

        // The two the endpoint needs, and no more: the version, so it refuses what it does not understand
        // rather than guessing, and the identifier that is the request.
        request.Headers.TryAddWithoutValidation(
            DonationEnvelope.FormatHeader,
            DonationEnvelope.CurrentFormat.ToString(CultureInfo.InvariantCulture));

        request.Headers.TryAddWithoutValidation(DonationEnvelope.DonorHeader, donor);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(Patience);

        var http = _http ?? CreateClient();

        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            var said = (await response.Content.ReadAsStringAsync(deadline.Token)).Trim();

            if (!response.IsSuccessStatusCode)
            {
                _log?.LogWarning(
                    "Erasure refused with {Status}: {Said}", (int)response.StatusCode, said);

                return ErasureOutcome.Refused(
                    $"The store refused the deletion with {(int)response.StatusCode}. Nothing is "
                    + "confirmed deleted, and the identifier on this machine has been kept so it "
                    + "can be asked again."
                    + (said is { Length: > 0 and < 300 } ? $" It said: {said}" : string.Empty));
            }

            return Erased(said);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return ErasureOutcome.Refused(
                "The store took longer than ten minutes to answer. Nothing is confirmed deleted.");
        }
        catch (OperationCanceledException)
        {
            return ErasureOutcome.Refused("Stopped. Nothing is confirmed deleted.");
        }
        catch (HttpRequestException ex)
        {
            _log?.LogWarning(ex, "Erasure could not reach {Endpoint}.", endpoint);
            return ErasureOutcome.Refused(
                "The donation address could not be reached, so nothing was deleted. The "
                + "identifier on this machine has been kept so it can be asked again.");
        }
        finally
        {
            if (_http is null)
            {
                http.Dispose();
            }
        }
    }

    /// <summary>What the store said it deleted.</summary>
    private static ErasureOutcome Erased(string said)
    {
        try
        {
            var answer = JsonDocument.Parse(said).RootElement;

            if (!answer.TryGetProperty("deleted", out var deleted)
                || deleted.ValueKind != JsonValueKind.Number)
            {
                return ErasureOutcome.Refused(
                    "The store answered something d47 could not read, so nothing is confirmed "
                    + "deleted. The identifier on this machine has been kept.");
            }

            var more = answer.TryGetProperty("more", out var left)
                       && left.ValueKind == JsonValueKind.True;

            var keys = answer.TryGetProperty("keys", out var named)
                       && named.ValueKind == JsonValueKind.Array
                ? named.EnumerateArray()
                    .Where(key => key.ValueKind == JsonValueKind.String)
                    .Select(key => key.GetString()!)
                    .ToArray()
                : [];

            return ErasureOutcome.Done(deleted.GetInt32(), more, keys);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return ErasureOutcome.Refused(
                "The store answered something d47 could not read, so nothing is confirmed "
                + "deleted. The identifier on this machine has been kept.");
        }
    }

    /// <summary>The object name the store says it used.</summary>
    private static string? KeyIn(string said)
    {
        try
        {
            return JsonDocument.Parse(said).RootElement.TryGetProperty("key", out var key)
                   && key.ValueKind == JsonValueKind.String
                ? key.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>What a refusal means, in words a Commander can act on.</summary>
    private static string Explain(HttpStatusCode status, string said)
    {
        var detail = said is { Length: > 0 and < 300 } ? $" It said: {said}" : string.Empty;

        return status switch
        {
            HttpStatusCode.RequestEntityTooLarge =>
                "The donation was too large for the endpoint and was refused before anything was "
                + "written. Choose a shorter span." + detail,
            HttpStatusCode.BadRequest =>
                "The endpoint did not understand this donation and refused it." + detail,
            HttpStatusCode.TooManyRequests or (HttpStatusCode)530 =>
                "The endpoint has taken all it will take today and refused this one. Nothing was "
                + "written; try tomorrow." + detail,
            _ => $"The endpoint refused it with {(int)status}. Nothing was written.{detail}",
        };
    }

    /// <summary>
    /// gzip, because the first corpus donation is 383 MB raw and 32.5 MB compressed and the difference
    /// is the difference between a feature and a thing nobody finishes uploading.
    /// </summary>
    private static byte[] Compress(ReadOnlySpan<byte> payload)
    {
        using var compressed = new MemoryStream();

        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(payload);
        }

        return compressed.ToArray();
    }
}
