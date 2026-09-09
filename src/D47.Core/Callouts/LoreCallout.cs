using D47.Core.Journal;
using D47.Core.Lore;

namespace D47.Core.Callouts;

/// <summary>
/// How much of the lore remark a Commander wants (Phase 23, "Look it up, and say where the answer came
/// from").
/// </summary>
public enum LoreRemarks
{
    /// <summary>Nothing is said on arrival.</summary>
    Off,

    /// <summary>The bare fact the book holds, and nothing further.</summary>
    Remark,

    /// <summary>The fact, and then whatever a web search turns up about the place.</summary>
    Lookup,
}

/// <summary>
/// Something about where you have just arrived, when there is something to say (Phase 23, "Remark on
/// arrival, and not again today").
/// </summary>
public sealed class LoreCallout(LoreBook book, LoreVisits visits) : ICallout
{
    public const string KeyPrefix = "lore.";

    /// <summary>The two events that mean "the Commander is somewhere new".</summary>
    private static readonly string[] Arrivals = ["FSDJump", "CarrierJump"];

    public string Id => "lore";

    /// <summary>How much to say.</summary>
    public Func<LoreRemarks> Remarks { get; set; } = () => LoreRemarks.Lookup;

    /// <summary>How long a system stays quiet after being remarked on.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Which system a remark was about, from its key — what the app needs to run the lookup for it.
    /// </summary>
    public static long? AddressOf(string key) =>
        key.StartsWith(KeyPrefix, StringComparison.Ordinal)
        && long.TryParse(key[KeyPrefix.Length..], out var address)
            ? address
            : null;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing to fold.
        if (context.IsPriming || Remarks() == LoreRemarks.Off)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (!Arrivals.Contains(journalEvent.Kind)
                || journalEvent.Long("SystemAddress") is not { } address)
            {
                continue;
            }

            // Absolute, and compared against the tick's own time.
            if (visits.LastSpokenAt(address) is { } last && context.Now - last < Window)
            {
                continue;
            }

            var entries = book.For(address);

            if (entries.Count == 0)
            {
                continue;
            }

            visits.Record(address, context.Now);

            yield return new Announcement(
                $"{KeyPrefix}{address}",
                string.Join(" ", entries.Select(entry => entry.Spoken())))
            {
                // Routine, and it never silences anything.
                Urgency = CalloutUrgency.Routine,

                // The 24-hour rule is the cooldown, and it lives in the visit store rather than in the
                // engine's table: the engine's is per session, and this one has to survive a restart or
                // logging off in a system means hearing about it again on return.
                Cooldown = TimeSpan.Zero,
            };
        }
    }
}
