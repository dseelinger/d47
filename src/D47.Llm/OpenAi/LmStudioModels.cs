using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Llm.OpenAi;

/// <summary>LM Studio's REST API for the context a model is loaded with, under the server root rather than <c>/v1</c>.</summary>
internal static class LmStudioModels
{
    private const string List = "/api/v1/models";
    private const string Unload = "/api/v1/models/unload";
    private const string Load = "/api/v1/models/load";

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LoadBudget = TimeSpan.FromMinutes(2);

    /// <summary>One reload at a time per server and model.</summary>
    private static readonly ConcurrentDictionary<(string Root, string Model), SemaphoreSlim> Gates = new();

    /// <summary>A model's entry in the list, and the id of its instance when exactly one is loaded.</summary>
    internal sealed record Listing(LoadedContext Context, string? InstanceId);

    /// <summary>
    /// The model's entry, or null when the server does not answer within 10 s or does not list it. Records the
    /// context when one instance is loaded.
    /// </summary>
    public static async Task<Listing?> ReadAsync(OpenAiEndpoint endpoint, string model, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ReadBudget);

        Listing? listing;

        try
        {
            using var response = await endpoint.GetFromRootAsync(List, budget.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            listing = Parse(await response.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false), model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }

        if (listing?.Context is { Instances: 1, Tokens: { } tokens })
        {
            EndpointDemotions.RecordContext(endpoint.BaseUrl, model, tokens);
        }

        return listing;
    }

    /// <summary>
    /// Unloads the model's one instance and loads it with <paramref name="tokens"/> of context. The caller's
    /// token is honoured only until the unload is sent.
    /// </summary>
    public static async Task<ContextReload> ReloadAsync(
        OpenAiEndpoint endpoint,
        string model,
        int tokens,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd((endpoint.Root, model), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var before = await ReadAsync(endpoint, model, cancellationToken).ConfigureAwait(false);

            if (before is null)
            {
                return new ContextReload(false, $"LM Studio at {endpoint.Host} did not list {model}.");
            }

            if (before.Context.Instances > 1)
            {
                return new ContextReload(
                    false,
                    string.Create(CultureInfo.InvariantCulture, $"{model} has {before.Context.Instances} instances loaded in LM Studio; d47 reloads a model only when it has one."));
            }

            if (tokens < 1 || (before.Context.Maximum > 0 && tokens > before.Context.Maximum))
            {
                return new ContextReload(
                    false,
                    string.Create(CultureInfo.InvariantCulture, $"{model} takes 1 to {before.Context.Maximum:N0} tokens of context, not {tokens:N0}."));
            }

            if (before.Context.Instances == 1)
            {
                // Without both, a failed load could not be undone.
                if (before.InstanceId is not { } instance || before.Context.Tokens is null)
                {
                    return new ContextReload(false, $"LM Studio did not report the loaded instance of {model} in full.");
                }

                if (await PostAsync(endpoint, Unload, InstanceBody(instance), ReadBudget).ConfigureAwait(false) is { } refused)
                {
                    return new ContextReload(false, refused);
                }
            }

            if (await PostAsync(endpoint, Load, LoadBody(model, tokens), LoadBudget).ConfigureAwait(false) is { } failed)
            {
                if (before.Context.Tokens is { } old
                    && await PostAsync(endpoint, Load, LoadBody(model, old), LoadBudget).ConfigureAwait(false) is { } restore)
                {
                    return new ContextReload(false, $"{failed} Loading it back at its old context also failed: {restore}");
                }

                return new ContextReload(false, failed);
            }

            var after = await ReadAsync(endpoint, model, CancellationToken.None).ConfigureAwait(false);

            return after?.Context is { Instances: 1, Tokens: { } loaded } && loaded >= tokens
                ? new ContextReload(true, null)
                : new ContextReload(
                    false,
                    after?.Context.Tokens is { } reported
                        ? string.Create(CultureInfo.InvariantCulture, $"LM Studio loaded {model} with {reported:N0} tokens of context, not {tokens:N0}.")
                        : $"LM Studio did not report {model} loaded after the reload.");
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The entry whose <c>key</c> is <paramref name="model"/>, or null when the body is not LM Studio's list.</summary>
    internal static Listing? Parse(string raw, string model)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("models", out var models)
                || models.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var entry in models.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || Text(entry, "key") != model)
                {
                    continue;
                }

                if (!entry.TryGetProperty("loaded_instances", out var instances) || instances.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var count = instances.GetArrayLength();

                // With several instances, the one whose id is the key, else the first.
                var chosen = instances.EnumerateArray().FirstOrDefault(instance => Text(instance, "id") == model);

                if (chosen.ValueKind == JsonValueKind.Undefined && count > 0)
                {
                    chosen = instances[0];
                }

                int? tokens = chosen.ValueKind == JsonValueKind.Object
                              && chosen.TryGetProperty("config", out var config)
                              && config.ValueKind == JsonValueKind.Object
                              && config.TryGetProperty("context_length", out var length)
                              && length.TryGetInt32(out var context)
                    ? context
                    : null;

                var maximum = entry.TryGetProperty("max_context_length", out var max) && max.TryGetInt32(out var most) ? most : 0;

                return new Listing(new LoadedContext(tokens, maximum, count), count == 1 ? Text(chosen, "id") : null);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Null when the server accepted the request, or what it said when it did not.</summary>
    private static async Task<string?> PostAsync(OpenAiEndpoint endpoint, string path, ReadOnlyMemory<byte> body, TimeSpan limit)
    {
        using var budget = new CancellationTokenSource(limit);

        try
        {
            using var response = await endpoint.PostToRootAsync(path, body, budget.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            var (detail, _, _) = await ChatCompletionsLlmProvider.ReadDetailAsync(response, budget.Token).ConfigureAwait(false);

            return detail ?? string.Create(CultureInfo.InvariantCulture, $"LM Studio answered {path} with status {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return endpoint.DescribeUnreachable(ex);
        }
    }

    internal static ReadOnlyMemory<byte> InstanceBody(string instance) =>
        Write(json => json.WriteString("instance_id", instance));

    internal static ReadOnlyMemory<byte> LoadBody(string model, int tokens) =>
        Write(json =>
        {
            json.WriteString("model", model);
            json.WriteNumber("context_length", tokens);
        });

    private static ReadOnlyMemory<byte> Write(Action<Utf8JsonWriter> fields)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            fields(json);
            json.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
