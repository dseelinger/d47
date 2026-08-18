namespace D47.Llm.OpenAi;

/// <summary>
/// The <c>data:</c> lines of a server-sent-event stream, one payload at a time.
/// <para>
/// Thirty lines rather than a dependency, and deliberately forgiving. Both OpenAI protocols use
/// only the <c>data:</c> field, so <c>event:</c>, <c>id:</c>, comments and anything else a server
/// invents are skipped rather than being errors — a compatible server that adds a keep-alive
/// comment every fifteen seconds is not malformed, and a decoder that says it is has failed at
/// the one job this half of the phase exists for.
/// </para>
/// </summary>
internal static class ServerSentEvents
{
    /// <summary>The sentinel Chat Completions ends with. Responses does not send one.</summary>
    private const string Done = "[DONE]";

    /// <summary>
    /// Each payload, in order, stopping at <c>[DONE]</c>.
    /// <para>
    /// A payload may be split across several <c>data:</c> lines in one event, which the
    /// specification says to join with newlines. Neither API does that today; it is handled
    /// anyway, because the cost is a StringBuilder and the failure it prevents is a truncated
    /// JSON object being reported as a malformed one.
    /// </para>
    /// </summary>
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

            // A blank line ends the event. Anything gathered so far is the payload.
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

            // One optional space after the colon, per the specification. Only one — the rest of
            // the line is the value, and JSON does not care but a sentinel comparison does.
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

        // A stream that ends without its final blank line. Chat Completions closes after [DONE]
        // and never gets here; a server that forgets the terminator does, and dropping the last
        // event of a turn because of a missing newline would lose the usage block.
        if (payload.Length > 0 && payload.ToString() is var last && last != Done)
        {
            yield return last;
        }
    }
}
