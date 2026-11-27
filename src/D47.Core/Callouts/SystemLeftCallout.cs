using System.Globalization;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// The biology and mapping still left in the system being jumped away from, said on a hyperspace
/// <c>StartJump</c> as counts. Bodies with a partly taken set are left to <see cref="UnfinishedSamplesCallout"/>.
/// </summary>
public sealed class SystemLeftCallout(CartographyLedger? ledger) : ICallout
{
    public string Id => "system-left";

    /// <summary>The least value, in credits, a planet is listed for mapping at.</summary>
    public Func<long> Threshold { get; set; } = () => 500_000;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming
            || context.State is not { } state
            || state.Location.SystemAddress is not { } address)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "StartJump" || journalEvent.String("JumpType") != "Hyperspace")
            {
                continue;
            }

            var items = SystemWorth.For(state, address, state.Location.StarPos, ledger, Threshold(), [])
                .Items
                .Where(item => !PartlySampled(state, address, item))
                .ToList();

            var mapping = items.Count(item => item.Kind == WorthKind.Mapping);
            var biology = items.Count(item => item.Kind == WorthKind.Biology);

            var parts = new List<string>();

            if (mapping > 0)
            {
                parts.Add($"{Words(mapping)} {(mapping == 1 ? "body" : "bodies")} worth mapping");
            }

            if (biology > 0)
            {
                parts.Add(mapping > 0
                    ? $"{Words(biology)} with biology"
                    : $"{Words(biology)} {(biology == 1 ? "body" : "bodies")} with biology");
            }

            if (parts.Count == 0)
            {
                continue;
            }

            yield return new Announcement(
                $"system-left.{address}.{journalEvent.Timestamp.Ticks}",
                $"Leaving {string.Join(" and ", parts)}.");
        }
    }

    private static bool PartlySampled(CommanderGameState state, long address, WorthItem item) =>
        item.BodyId is { } body
        && state.Sampling.On(address, body) is { } sampling
        && sampling.InProgress.Any(genus => genus.Taken > 0);

    private static string Words(int count) => SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture));
}
