namespace D47.Llm.OpenAi;

/// <summary>The <c>data:</c> lines of a server-sent-event stream, one payload at a time.</summary>
internal static class ServerSentEvents
{
    /// <summary>The sentinel Chat Completions ends with.</summary>
    private const string Done = "[DONE]";

    /// <summary>Each payload, in order, stopping at <c>[DONE]</c>.</summary>
    public static async IAsyncEnumerable<string> ReadAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        var payload = new System.Text.StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                break;
            }

            // A blank line ends the event.
            if (line.Length == 0)
            {
                if (payload.Length > 0)
                {
                    var complete = payload.ToString();
                    payload.Clear();

                    if (complete == Done)
                    {
                        yield break;
                    }

                    yield return complete;
                }

                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            // One optional space after the colon, per the specification.
            var value = line[5..];

            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            if (payload.Length > 0)
            {
                payload.Append('\n');
            }

            payload.Append(value);
        }

        // A stream that ends without its final blank line.
        if (payload.Length > 0 && payload.ToString() is var last && last != Done)
        {
            yield return last;
        }
    }
}
