using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Capabilities.Builtin;
using D47.Core.Persona;

namespace D47.Core.Conversation;

/// <summary>A message said to everyone on a comms channel: the call that chooses who answers, and what each answerer is asked.</summary>
public static class AllOnComms
{
    /// <summary>How many parties answer one message at most.</summary>
    public const int MostAnswering = 3;

    /// <summary>How long the routing call may run on a provider that is not on this machine.</summary>
    public static readonly TimeSpan RoutingLimit = TimeSpan.FromSeconds(10);

    private const string SchemaName = "comms_answerers";

    /// <summary>One party that heard the message and whose line would answer it.</summary>
    public sealed record Candidate(string Key, string Name, string Brief);

    /// <summary>The routing call's instruction.</summary>
    public static string Instruction(CommsChannel channel, string asked, IReadOnlyList<Candidate> candidates, IReadOnlyList<string> lastExchange)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(lastExchange);

        var parties = string.Join('\n', candidates.Select(candidate => $"- {candidate.Key}: {candidate.Name}. {FirstSentence(candidate.Brief)}".TrimEnd()));
        var before = lastExchange.Count == 0
            ? string.Empty
            : $"\nThe last message said to everyone on this channel, oldest first:\n{ExchangeLines.Recent(lastExchange)}\n";

        return $$"""
                 The Commander spoke over comms to everyone on the {{CommsCapability.ChannelNames[channel]}} channel and said: "{{asked.Trim()}}"

                 These parties heard it. Each line gives the party's key, name and role:
                 {{parties}}
                 {{before}}
                 Choose who answers. If the Commander addressed a party by name, choose that party. Otherwise choose the
                 parties whose job the message concerns. Choose one party unless several have something different to add,
                 and list them in the order they should speak.

                 Answer with JSON only, in the form {"answer": ["key"]}, using keys from the list above.
                 """;
    }

    /// <summary>The routing call's answer schema: an array of the candidates' keys.</summary>
    public static LlmResponseSchema Schema(IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var keys = new JsonArray([.. candidates.Select(candidate => (JsonNode)JsonValue.Create(candidate.Key)!)]);

        return new LlmResponseSchema(
            SchemaName,
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["answer"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string", ["enum"] = keys },
                    },
                },
                ["required"] = new JsonArray("answer"),
                ["additionalProperties"] = false,
            }.ToJsonString());
    }

    /// <summary>
    /// The indexes of the candidates the reply names, in its order, without repeats and at most <see cref="MostAnswering"/>;
    /// the first candidate when it names none.
    /// </summary>
    public static IReadOnlyList<int> Chosen(string? reply, IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<int> chosen = [];

        foreach (var key in Keys(reply))
        {
            var index = -1;

            for (var i = 0; i < candidates.Count && index < 0; i++)
            {
                if (string.Equals(candidates[i].Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                }
            }

            if (index >= 0 && !chosen.Contains(index))
            {
                chosen.Add(index);
            }

            if (chosen.Count == MostAnswering)
            {
                break;
            }
        }

        return chosen.Count > 0 ? chosen : [0];
    }

    /// <summary>What a party answering the message is asked, with the replies already given.</summary>
    public static string Question(CommsChannel channel, string question, IReadOnlyList<CommsReply> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);

        var asked = $"The Commander, to everyone on the {CommsCapability.ChannelNames[channel]} channel: \"{question.Trim()}\"";
        var replies = answered.Where(reply => reply.Said.Trim().Length > 0).ToList();

        return replies.Count == 0
            ? asked
            : $"{asked}\n\nAlready answered:\n{string.Join('\n', replies.Select(reply => $"- {reply.Name}: \"{reply.Said.Trim()}\""))}\n"
              + "Add only what is yours to add; do not repeat them.";
    }

    /// <summary>The first sentence of <paramref name="brief"/>, with its whitespace collapsed.</summary>
    public static string FirstSentence(string brief)
    {
        ArgumentNullException.ThrowIfNull(brief);

        var text = string.Join(' ', brief.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var end = text.IndexOf(". ", StringComparison.Ordinal);

        return end < 0 ? text : text[..(end + 1)];
    }

    private static IReadOnlyList<string> Keys(string? reply)
    {
        if (reply is null || Adventures.AdventureGenerator.Unfence(reply) is not { } json)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });

            return document.RootElement is { ValueKind: JsonValueKind.Object } root
                   && root.TryGetProperty("answer", out var answer)
                   && answer.ValueKind == JsonValueKind.Array
                ? [.. answer.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
