using System.Text.Json;

namespace D47.Knowledge;

internal static class KnowledgeHttp
{
    /// <summary>The injected client, or a new one with <paramref name="timeout"/>, given the host's base address and the d47 user agent where unset.</summary>
    public static HttpClient Client(HttpClient? http, string host, TimeSpan timeout)
    {
        var client = http ?? new HttpClient { Timeout = timeout };

        client.BaseAddress ??= new Uri($"https://{host}/");

        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }

        return client;
    }

    /// <summary>The body as JSON, or null when it says or turns out to be longer than <paramref name="mostBytes"/>; read the response with headers only.</summary>
    public static async Task<JsonDocument?> ReadJsonAtMostAsync(HttpResponseMessage response, long mostBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > mostBytes)
        {
            return null;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using var body = new MemoryStream();
            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (body.Length + read > mostBytes)
                {
                    return null;
                }

                body.Write(chunk, 0, read);
            }

            body.Position = 0;
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>"a", "a and b", "a, b and c".</summary>
    public static string Listed(IReadOnlyList<string> items) =>
        items.Count < 2 ? string.Concat(items) : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";
}
