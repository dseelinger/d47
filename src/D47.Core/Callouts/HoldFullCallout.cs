using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The hold reaching capacity during a mining run, said once per run (#609).</summary>
public sealed class HoldFullCallout : ICallout
{
    private DateTimeOffset? _spokenFor;

    public string Id => "hold-full";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { Mining.Open: { } run } state
            || state.Ship.CargoCapacity is not { } capacity
            || capacity <= 0
            || state.Hold is not { IsKnown: true, IsShip: true } hold
            || hold.Count < capacity
            || _spokenFor == run.OpenedAt)
        {
            yield break;
        }

        _spokenFor = run.OpenedAt;

        if (context.IsPriming)
        {
            yield break;
        }

        yield return new Announcement(
            $"hold-full.{run.OpenedAt.Ticks}",
            run.TonnesRefined == 0
                ? "Hold full."
                : $"Hold full. {MiningSummaryCallout.Tonnes(run.TonnesRefined)} refined this run.");
    }
}
