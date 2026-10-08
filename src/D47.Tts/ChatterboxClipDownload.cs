namespace D47.Tts;

/// <summary>Fetches one Chatterbox reference clip, refusing a body longer than the catalogue says.</summary>
internal static class ChatterboxClipDownload
{
    private static readonly HttpClient Http = CreateClient();

    public static async Task<byte[]> GetAsync(Uri url, long bytes, CancellationToken cancellationToken)
    {
        using var response = await Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"the release answered {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        if (response.Content.Headers.ContentLength is { } length && length != bytes)
        {
            throw new InvalidDataException($"the release offered {length} bytes, not {bytes}");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        // One byte over, so a longer body is caught without reading the rest of it.
        var buffer = new byte[bytes + 1];
        var read = 0;

        while (read < buffer.Length)
        {
            var got = await body.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);

            if (got == 0)
            {
                break;
            }

            read += got;
        }

        if (read != bytes)
        {
            throw new InvalidDataException(read > bytes
                ? $"the release sent more than {bytes} bytes"
                : $"the release sent {read} bytes, not {bytes}");
        }

        return buffer[..read];
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("D47");

        return client;
    }
}
