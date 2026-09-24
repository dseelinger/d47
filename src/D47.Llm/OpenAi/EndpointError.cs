using System.Globalization;
using System.Text.Json;

namespace D47.Llm.OpenAi;

/// <summary>An endpoint's <c>error</c> object, as a sentence the Commander can act on.</summary>
internal static class EndpointError
{
    private const int MaxDepth = 4;

    /// <summary>
    /// The innermost <c>message</c> of <paramref name="error"/>, following JSON embedded after a prefix, or a
    /// context overflow stated with both token counts and the setting that fixes it.
    /// </summary>
    public static string? Describe(JsonElement error, string host)
    {
        string? message = null;

        foreach (var layer in Layers(error))
        {
            if (Overflow(layer, host) is { } overflow)
            {
                return overflow;
            }

            message = Text(layer, "message") ?? message;
        }

        return message;
    }

    /// <summary>The context size an overflow refusal names, following the same nesting as <see cref="Describe"/>.</summary>
    public static int? ContextSize(JsonElement error)
    {
        foreach (var layer in Layers(error))
        {
            if (IsOverflow(layer) && Number(layer, "n_ctx") is > 0 and <= int.MaxValue and var context)
            {
                return (int)context;
            }
        }

        return null;
    }

    /// <summary><paramref name="error"/>, then each error object embedded in the one before's <c>message</c>.</summary>
    private static IEnumerable<JsonElement> Layers(JsonElement error)
    {
        var current = error;

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            yield return current;

            if (Nested(Text(current, "message")) is not { } inner)
            {
                yield break;
            }

            current = inner;
        }
    }

    /// <summary>The error object embedded in <paramref name="message"/> after a prefix, if there is one.</summary>
    private static JsonElement? Nested(string? message)
    {
        var start = message?.IndexOf('{', StringComparison.Ordinal) ?? -1;
        var end = message?.LastIndexOf('}') ?? -1;

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(message![start..(end + 1)]);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                ? error.Clone()
                : root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Overflow(JsonElement error, string host)
    {
        var context = Number(error, "n_ctx");
        var needed = Number(error, "n_prompt_tokens");

        if (!IsOverflow(error) || context <= 0 || needed <= 0)
        {
            return null;
        }

        var suggested = 1L;

        while (suggested < needed)
        {
            suggested *= 2;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The model at {host} has a context of {context:N0} tokens and this request needed {needed:N0}. "
            + $"Raise the model's context length in your server's settings — {suggested:N0} or more.");
    }

    private static bool IsOverflow(JsonElement error) => Text(error, "type") == "exceed_context_size_error";

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
