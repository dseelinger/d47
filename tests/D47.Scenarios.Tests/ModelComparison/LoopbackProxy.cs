using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// A proxy on a loopback port in front of a model server on this machine. Each chat request is rewritten for the
/// variant, then the request body and the streamed reply are saved, numbered, in <c>folder</c>.
/// </summary>
/// <remarks>
/// The variant is a comma-separated list: <c>effort=omit</c> drops <c>reasoning_effort</c>, <c>effort=X</c> sets it,
/// <c>schema</c> adds a <c>response_format</c> JSON schema written from the instruction text, <c>temperature=X</c>
/// sets the temperature, and <c>max=N</c> caps the output token limit at N.
/// </remarks>
[Trait("Category", "Integration")]
public sealed partial class LoopbackProxy : IAsyncDisposable
{
    private readonly HttpListener _listener = new();

    private readonly HttpClient _client = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly Uri _target;

    private readonly string _variant;

    private readonly string _folder;

    private readonly CancellationTokenSource _stop = new();

    private readonly Task _loop;

    private int _numbered;

    public LoopbackProxy(int port, string target, string variant, string folder)
    {
        _target = new Uri(target.TrimEnd('/') + "/");
        _variant = variant;
        _folder = folder;
        Directory.CreateDirectory(folder);
        _listener.Prefixes.Add($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/");
        _listener.Start();
        _loop = Task.Run(ListenAsync);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (HttpListenerException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _listener.Close();
        _client.Dispose();
        _stop.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var number = Interlocked.Increment(ref _numbered);
        var stem = Path.Combine(_folder, number.ToString("000", CultureInfo.InvariantCulture));
        var clock = Stopwatch.StartNew();
        var path = context.Request.Url!.PathAndQuery.TrimStart('/');

        try
        {
            string? body = null;

            if (context.Request.HasEntityBody)
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                body = await reader.ReadToEndAsync(_stop.Token).ConfigureAwait(false);
            }

            string? schema = null;

            if (body is not null && path.EndsWith("chat/completions", StringComparison.Ordinal))
            {
                (body, schema) = Rewrite(body, _variant);
                await File.WriteAllTextAsync(stem + "-request.json", body, _stop.Token).ConfigureAwait(false);
            }

            using var forward = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), new Uri(_target, path));

            if (body is not null)
            {
                forward.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var answer = await _client.SendAsync(forward, HttpCompletionOption.ResponseHeadersRead, _stop.Token).ConfigureAwait(false);
            context.Response.StatusCode = (int)answer.StatusCode;
            context.Response.ContentType = answer.Content.Headers.ContentType?.ToString();
            context.Response.SendChunked = true;

            await using var upstream = await answer.Content.ReadAsStreamAsync(_stop.Token).ConfigureAwait(false);
            await using var saved = File.Create(stem + "-response.txt");
            var output = context.Response.OutputStream;
            var buffer = new byte[16 * 1024];
            long bytes = 0;
            int read;

            while ((read = await upstream.ReadAsync(buffer, _stop.Token).ConfigureAwait(false)) > 0)
            {
                await saved.WriteAsync(buffer.AsMemory(0, read), _stop.Token).ConfigureAwait(false);
                await output.WriteAsync(buffer.AsMemory(0, read), _stop.Token).ConfigureAwait(false);
                await output.FlushAsync(_stop.Token).ConfigureAwait(false);
                bytes += read;
            }

            context.Response.Close();
            Index(number, path, (int)answer.StatusCode, clock.Elapsed.TotalSeconds, bytes, schema, null);
        }
        catch (Exception exception)
        {
            Index(number, path, 0, clock.Elapsed.TotalSeconds, 0, null, $"{exception.GetType().Name}: {exception.Message}");

            try
            {
                context.Response.StatusCode = 502;
                context.Response.Close();
            }
            catch (Exception closing) when (closing is HttpListenerException or InvalidOperationException or ObjectDisposedException)
            {
            }
        }
    }

    private void Index(int number, string path, int status, double seconds, long bytes, string? schema, string? failure)
    {
        var line = new JsonObject
        {
            ["n"] = number,
            ["at"] = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            ["path"] = path,
            ["variant"] = _variant,
            ["schema"] = schema,
            ["status"] = status,
            ["seconds"] = Math.Round(seconds, 2),
            ["bytes"] = bytes,
            ["failure"] = failure,
        };

        lock (_listener)
        {
            File.AppendAllText(Path.Combine(_folder, "index.jsonl"), line.ToJsonString() + Environment.NewLine);
        }
    }

    /// <summary>The request body rewritten for <paramref name="variant"/>, and the name of any schema added.</summary>
    internal static (string Body, string? Schema) Rewrite(string body, string variant)
    {
        var request = JsonNode.Parse(body)!.AsObject();
        string? schemaName = null;

        foreach (var part in variant.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (key, value) = part.IndexOf('=', StringComparison.Ordinal) is var at and >= 0 ? (part[..at], part[(at + 1)..]) : (part, "");

            switch (key)
            {
                case "effort" when value == "omit":
                    request.Remove("reasoning_effort");
                    break;
                case "effort":
                    request["reasoning_effort"] = value;
                    break;
                case "temperature":
                    request["temperature"] = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "max":
                    var cap = int.Parse(value, CultureInfo.InvariantCulture);

                    foreach (var field in new[] { "max_tokens", "max_completion_tokens" })
                    {
                        if (request[field] is { } limit)
                        {
                            request[field] = Math.Min(limit.GetValue<int>(), cap);
                        }
                    }

                    break;
                case "schema":
                    var instruction = string.Join("\n", request["messages"]!.AsArray().Select(message => message?["content"] is JsonValue content ? content.GetValue<string>() : ""));

                    if (StorySchema(instruction) is var (name, schema))
                    {
                        schemaName = name;
                        request["response_format"] = new JsonObject
                        {
                            ["type"] = "json_schema",
                            ["json_schema"] = new JsonObject { ["name"] = name, ["strict"] = true, ["schema"] = schema },
                        };
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown proxy variant part \"{part}\".");
            }
        }

        return (request.ToJsonString(), schemaName);
    }

    /// <summary>
    /// The JSON schema for the shape the instruction asks for: the spine's six strings, or the beats object with
    /// the kinds, speakers and destination the instruction names. Null for any other instruction.
    /// </summary>
    internal static (string Name, JsonObject Schema)? StorySchema(string instruction)
    {
        if (instruction.Contains("Answer with one JSON object and nothing else, with these string fields: \"name\"", StringComparison.Ordinal))
        {
            return ("story_spine", Strict(Strings("name", "premise", "want", "stake", "turn", "ending")));
        }

        if (!instruction.Contains("\"beats\": [{\"title\": string", StringComparison.Ordinal))
        {
            return null;
        }

        var speakers = instruction.Contains("\"opening\": [{\"text\": string, \"speaker\": string}]", StringComparison.Ordinal);
        var kinds = KindList().Match(instruction) is { Success: true } match
            ? Quoted().Matches(match.Groups[1].Value).Select(kind => (JsonNode?)kind.Groups[1].Value).ToArray()
            : throw new InvalidOperationException("The beats instruction names no kinds.");

        JsonNode Said() => new JsonObject
        {
            ["type"] = "array",
            ["items"] = Strict(Strings("text", "speaker")),
        };

        var beat = Strict(Strings("title", "function"));
        var beatProperties = beat["properties"]!.AsObject();
        beatProperties["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(kinds) };

        foreach (var field in new[] { "reason", "system", "station", "body", "career", "ship", "faction", "mission", "commodity", "filter", "engineer", "stage" })
        {
            beatProperties[field] = Nullable("string");
        }

        beatProperties["rank"] = Nullable("number");
        beatProperties["count"] = Nullable("number");
        beatProperties["organic"] = Nullable("boolean");
        beatProperties[speakers ? "lines" : "line"] = speakers ? Said() : new JsonObject { ["type"] = "string" };
        beat["required"] = new JsonArray(beatProperties.Select(property => (JsonNode?)property.Key).ToArray());

        var root = Strict(Strings("name", "premise", "want", "stake", "turn", "ending"));
        var rootProperties = root["properties"]!.AsObject();
        rootProperties["opening"] = speakers ? Said() : new JsonObject { ["type"] = "string" };
        rootProperties["reply"] = new JsonObject { ["type"] = "string" };
        rootProperties["beats"] = new JsonObject { ["type"] = "array", ["items"] = beat };

        if (instruction.Contains("\"destination\": {\"system\": string, \"body\": string}", StringComparison.Ordinal))
        {
            rootProperties["destination"] = Strict(Strings("system", "body"));
        }

        root["required"] = new JsonArray(rootProperties.Select(property => (JsonNode?)property.Key).ToArray());
        return ("story_beats", root);
    }

    private static JsonObject Strings(params string[] names)
    {
        var properties = new JsonObject();

        foreach (var name in names)
        {
            properties[name] = new JsonObject { ["type"] = "string" };
        }

        return new JsonObject { ["type"] = "object", ["properties"] = properties };
    }

    private static JsonObject Strict(JsonObject schema)
    {
        schema["required"] = new JsonArray(schema["properties"]!.AsObject().Select(property => (JsonNode?)property.Key).ToArray());
        schema["additionalProperties"] = false;
        return schema;
    }

    private static JsonObject Nullable(string type) => new() { ["type"] = new JsonArray(type, "null") };

    [GeneratedRegex("\"kind\": ((?:\"[a-z]+\"\\|?)+)")]
    private static partial Regex KindList();

    [GeneratedRegex("\"([a-z]+)\"")]
    private static partial Regex Quoted();
}
