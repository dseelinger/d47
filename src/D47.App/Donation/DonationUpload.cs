using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Diagnostics.Donation;
using Microsoft.Extensions.Logging;

namespace D47.App.Donation;

/// <summary>
/// One POST, to one address, carrying one donation
/// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>).
/// <para>
/// <b>An <see cref="HttpClient"/> and nothing else.</b> No SDK, no package: the licence gate walks
/// the transitive graph, and a storage SDK would drag a subtree through it to save writing seven
/// headers. The endpoint is a Worker in front of a bucket, and the Worker is the only writer —
/// there is no bucket credential in this binary and no path from here to storage that does not go
/// through code that can refuse.
/// </para>
/// <para>
/// <b>One attempt, no retry.</b> A donation is an act a Commander performed, and a retry is that
/// act performed again without being asked — which for a 32 MB payload against a daily request
/// ceiling is also how one press becomes several. A failure is reported in words and the window
/// still has a clipboard and a file.
/// </para>
/// <para>
/// <b>Nothing here decides anything.</b> It does not choose an address, mint a token, or work out
/// whether consent was given; it is handed an envelope and a body and it posts them. That is what
/// keeps the decision in front of the Commander and out of the network layer.
/// </para>
/// </summary>
public sealed class DonationUpload
{
    /// <summary>
    /// The path a donation is posted to, appended to the configured address. Fixed rather than
    /// configurable: the setting names a host, and a host that can be pointed at an arbitrary path
    /// is a wider thing than the row claims to be.
    /// </summary>
    public const string Path = "donate";

    /// <summary>
    /// How long one donation may take. Generous, because the first corpus donation is tens of
    /// megabytes over whatever connection the Commander has — and bounded, because a request that
    /// never returns leaves a window saying "sending" forever.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

    private readonly HttpClient? _http;
    private readonly ILogger? _log;

    /// <param name="http">
    /// The client, or null to make one per send. <b>Injected so a test can answer without a
    /// network</b>; null is the production case, and one client per donation is the same trade
    /// <see cref="Updates.UpdateChecker"/> already makes — a donation happens when somebody presses
    /// a button, so there is no pool worth keeping and nothing to exhaust.
    /// </param>
    public DonationUpload(HttpClient? http = null, ILogger? log = null)
    {
        _http = http;
        _log = log;
    }

    /// <summary>
    /// The client used when none was injected. <b>No timeout of its own</b>: a 32 MB upload over a
    /// domestic connection outlives every default, and the deadline that actually bounds this is
    /// <see cref="Patience"/>, which is one number rather than two that can disagree.
    /// </summary>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-donation");
        return client;
    }

    /// <summary>
    /// Whether an address is one this will post to. <b>https and nothing else</b>: the payload is
    /// a scrubbed journal, and a plaintext destination would put it on the wire for anybody on the
    /// path — which is a worse failure than not donating.
    /// </summary>
    public static bool IsUsable(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Where a request actually goes, for the disclosure and for the receipt.</summary>
    public static string Destination(string endpoint) =>
        new Uri(new Uri(endpoint.TrimEnd('/') + "/"), Path).ToString();

    /// <summary>
    /// Sends it. <b>Every way this can go wrong comes back as a sentence rather than an
    /// exception</b> — a donation window is not a place to surface a socket error, and the one
    /// thing worse than a donation that did not arrive is one that silently did not.
    /// </summary>
    /// <param name="endpoint">The configured address. Checked with <see cref="IsUsable"/> first.</param>
    /// <param name="envelope">What is written on the outside. Sent as headers, never in the body.</param>
    /// <param name="payload">
    /// The scrubbed payload, exactly as consented to. <b>Compressed here and nowhere else</b>: the
    /// hash on the envelope is over these bytes rather than over what goes on the wire, so
    /// compression can change without changing what a donor can check.
    /// </param>
    public async Task<DonationOutcome> SendAsync(
        string endpoint,
        DonationEnvelope envelope,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancel = default)
    {
        if (!IsUsable(endpoint))
        {
            return DonationOutcome.Refused(
                "The donation address is not an https address, so nothing was sent.");
        }

        if (!envelope.IsWellFormed())
        {
            // Refused here rather than by the endpoint. A request that is going to be rejected has
            // still spent one against a ceiling that is the whole reason this design cannot bill.
            return DonationOutcome.Refused(
                "d47 could not assemble a complete donation envelope, so nothing was sent.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Destination(endpoint));

        foreach (var (name, value) in envelope.Headers())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        request.Content = new ByteArrayContent(Compress(payload.Span));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(Patience);

        // Made here rather than held, when nobody handed one in. Disposed with the request, which
        // is the whole of this class's business with a socket.
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

    /// <summary>
    /// The object name the store says it used.
    /// <para>
    /// <b>Read rather than assumed, and the envelope's own prediction is the fallback.</b> The
    /// endpoint derives the key itself — a client that names its own object names a path inside
    /// somebody else's bucket — so what comes back is the authority, and a response d47 cannot
    /// parse must not turn a stored donation into a failed one.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What a refusal means, in words a Commander can act on. <b>The endpoint's own text is quoted
    /// rather than shown alone</b>: it is written for whoever is reading a log, and 413 on its own
    /// tells nobody to choose a shorter span.
    /// </summary>
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
    /// gzip, because the first corpus donation is 383 MB raw and 32.5 MB compressed and the
    /// difference is the difference between a feature and a thing nobody finishes uploading.
    /// <para>
    /// <b>The stored object is the compressed bytes.</b> The store is billed by what it holds and
    /// this is journal text, which compresses about twelve to one — so what is decompressed is
    /// decompressed by whoever replays it, once, rather than by a Worker that would then have to
    /// defend itself against a decompression bomb for no gain.
    /// </para>
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
