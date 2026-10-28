namespace D47.Core.Journal;

/// <summary>Reads commands the Commander typed into Elite's local chat from <c>SendText</c> events.</summary>
public static class ChatBoxCommands
{
    public const string Prefix = "/d47";

    private const string Channel = "local";

    /// <summary>The command in each <c>SendText</c> to local that starts with <see cref="Prefix"/> and a space.</summary>
    public static IEnumerable<string> Read(IEnumerable<JournalEvent> events)
    {
        foreach (var journalEvent in events)
        {
            if (Read(journalEvent) is { } command)
            {
                yield return command;
            }
        }
    }

    public static string? Read(JournalEvent journalEvent)
    {
        if (journalEvent.Kind != "SendText"
            || !string.Equals(journalEvent.String("To"), Channel, StringComparison.OrdinalIgnoreCase)
            || journalEvent.String("Message") is not { } message
            || !StartsWithPrefix(message, requireCommand: true))
        {
            return null;
        }

        return message[Prefix.Length..].Trim();
    }

    /// <summary>Whether <paramref name="message"/> is the prefix alone or the prefix and whitespace.</summary>
    public static bool StartsWithPrefix(string message) => StartsWithPrefix(message, requireCommand: false);

    private static bool StartsWithPrefix(string message, bool requireCommand)
    {
        if (!message.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = message[Prefix.Length..];

        return requireCommand
            ? rest.Length > 0 && char.IsWhiteSpace(rest[0]) && rest.Trim().Length > 0
            : rest.Length == 0 || char.IsWhiteSpace(rest[0]);
    }
}
