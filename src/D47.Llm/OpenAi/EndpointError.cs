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
        var current = error;
        string? message = null;

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (Overflow(current, host) is { } overflow)
            {
                return overflow;
            }

            message = Text(current, "message") ?? message;

            if (Nested(message) is not { } inner)
            {
                break;
            }

            current = inner;
        }

        return message;
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

        if (Text(error, "type") != "exceed_context_size_error" || context <= 0 || needed <= 0)
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

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
