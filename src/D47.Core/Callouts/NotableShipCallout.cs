using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A remark from the core aboard when a notable ship hails nearby, named by the family of the message's
/// <c>$</c>-key, once per kind per game session (#1444).
/// </summary>
public sealed class NotableShipCallout : ICallout
{
    /// <summary>Every notable-ship key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "notable.";

    /// <summary>The message families that name a notable ship, and the kind and authored line for each.</summary>
    private static readonly Dictionary<string, (string Kind, string Line)> Families = new(StringComparer.Ordinal)
    {
        ["ConvoyWedding"] = ("wedding convoy", "A wedding convoy is passing close by."),
        ["ConvoyFuneral"] = ("funeral procession", "A funeral procession is passing close by."),
        ["ConvoyNomads"] = ("nomad convoy", "A nomad convoy is passing close by."),
        ["RefugeeFlotillaWar"] = ("refugee flotilla fleeing a war", "A refugee flotilla fleeing a war is passing close by."),
        ["PassengerLiner"] = ("tour liner", "A tour liner is passing through."),
        ["Protester"] = ("protester", "A protester is nearby."),
        ["Propagandist"] = ("propagandist", "A propagandist is nearby."),
        ["PoliceAcademy"] = ("security academy training flight", "A security academy training flight is nearby."),
    };

    private readonly HashSet<string> _spoken = new(StringComparer.Ordinal);

    public string Id => "notable-ships";

    /// <summary>The kind named by a brief's announcement text, or null when the text is not an authored line.</summary>
    internal static string? KindOf(string text) =>
        Families.Values.FirstOrDefault(entry => entry.Line == text).Kind;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame" when journalEvent.StartsSession:
                    _spoken.Clear();
                    break;

                case "ReceiveText" when !context.IsPriming
                    && string.Equals(journalEvent.String("Channel"), "npc", StringComparison.OrdinalIgnoreCase)
                    && journalEvent.String("Message") is { Length: > 1 } message
                    && message[0] == '$'
                    && Families.TryGetValue(IncomingMessages.FamilyOf(message), out var notable)
                    && _spoken.Add(notable.Kind):
                    yield return new Announcement(KeyPrefix + notable.Kind.Replace(' ', '-'), notable.Line);
                    break;
            }
        }
    }
}
