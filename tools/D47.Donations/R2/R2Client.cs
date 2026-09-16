using System.Globalization;
using System.Net;
using System.Xml.Linq;
using D47.Donations.Store;

namespace D47.Donations.R2;

/// <summary>
/// Reads <c>d47-donations</c> over R2's S3-compatible API. It lists, heads and gets, and there is no
/// write path here at all: the Worker stays the only writer, which is what the privacy notice says.
/// </summary>
public sealed class R2Client : IDisposable
{
    public const string Bucket = "d47-donations";

    private readonly R2Credentials _credentials;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public R2Client(R2Credentials credentials, TimeProvider? time = null, HttpMessageHandler? handler = null)
    {
        _credentials = credentials;
        _time = time ?? TimeProvider.System;

        // No automatic decompression: an object is stored gzipped and says so, and a handler that
        // unpacked it invisibly would make "decompress exactly once" impossible to see in the code.
        _http = new HttpClient(
            handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None },
            disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
    }

    /// <summary>
    /// Every donation in the bucket. A key the Worker did not write is skipped rather than shown.
    /// </summary>
    public async Task<IReadOnlyList<StoredObject>> ListAsync(CancellationToken cancel)
    {
        var objects = new List<StoredObject>();
        string? continuation = null;

        do
        {
            var query = new List<(string Name, string Value)> { ("list-type", "2") };

            if (continuation is not null)
            {
                query.Add(("continuation-token", continuation));
            }

            using var response = await SendAsync(HttpMethod.Get, $"/{Bucket}", query, cancel);
            var listing = XDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
            var root = listing.Root ?? throw new R2Exception(response.StatusCode, "Unknown", "Empty listing.");

            foreach (var contents in root.Elements().Where(element => element.Name.LocalName == "Contents"))
            {
                if (Child(contents, "Key") is not { } key || !DonationKey.TryParse(key, out var parsed))
                {
                    continue;
                }

                objects.Add(new StoredObject(
                    parsed,
                    long.Parse(Child(contents, "Size") ?? "0", CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(
                        Child(contents, "LastModified") ?? string.Empty,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind)));
            }

            continuation = string.Equals(Child(root, "IsTruncated"), "true", StringComparison.OrdinalIgnoreCase)
                ? Child(root, "NextContinuationToken")
                : null;
        }
        while (continuation is not null);

        return objects;
    }

    /// <summary>The object's custom metadata, which a listing does not carry.</summary>
    public async Task<IReadOnlyDictionary<string, string>> HeadAsync(string key, CancellationToken cancel)
    {
        using var response = await SendAsync(HttpMethod.Head, $"/{Bucket}/{key}", [], cancel);

        return MetadataOf(response);
    }

    /// <summary>The object as stored, still compressed, with its metadata.</summary>
    public async Task<FetchedObject> GetAsync(string key, CancellationToken cancel)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/{Bucket}/{key}", [], cancel);

        return new FetchedObject(
            await response.Content.ReadAsByteArrayAsync(cancel),
            string.Equals(
                response.Content.Headers.ContentEncoding.FirstOrDefault(),
                "gzip",
                StringComparison.OrdinalIgnoreCase),
            MetadataOf(response));
    }

    public void Dispose() => _http.Dispose();

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<(string Name, string Value)> query,
        CancellationToken cancel)
    {
        var when = _time.GetUtcNow();
        var canonicalQuery = SignatureV4.CanonicalQuery(query);

        // Built from the same canonical strings the signature is over, so the request cannot be
        // signed in one spelling and sent in another.
        var url = $"https://{_credentials.Host}{SignatureV4.CanonicalPath(path)}"
                  + (canonicalQuery.Length > 0 ? $"?{canonicalQuery}" : string.Empty);

        using var request = new HttpRequestMessage(method, url);

        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", SignatureV4.EmptyPayloadSha256);
        request.Headers.TryAddWithoutValidation("x-amz-date", SignatureV4.AmzDate(when));
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            SignatureV4.Authorization(_credentials, method.Method, path, query, _credentials.Host, when));

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        // A HEAD has no body to read an error document out of, so its status is the whole of it.
        var body = method == HttpMethod.Head ? string.Empty : await response.Content.ReadAsStringAsync(cancel);
        var status = response.StatusCode;

        response.Dispose();

        throw R2Exception.From(status, body);
    }

    private static IReadOnlyDictionary<string, string> MetadataOf(HttpResponseMessage response) =>
        response.Headers
            .Concat(response.Content.Headers)
            .Where(header => header.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                header => header.Key[MetadataPrefix.Length..],
                header => string.Join(',', header.Value),
                StringComparer.OrdinalIgnoreCase);

    private const string MetadataPrefix = "x-amz-meta-";

    private static string? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;
}

/// <summary>What a GET returned, before anything has been decompressed.</summary>
public sealed record FetchedObject(
    byte[] Body,
    bool Gzipped,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>The SHA-256 the Worker stored, which is over the decompressed payload.</summary>
    public string? Sha256 => Metadata.GetValueOrDefault("sha256");

    public string? Build => Metadata.GetValueOrDefault("build");
}
