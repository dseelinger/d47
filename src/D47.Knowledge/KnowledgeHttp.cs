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
}
