using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Warns on a hyperspace jump that salvage scooped in the pledged Power's system is still unclaimed.</summary>
public sealed class PowerplaySalvageCallout : ICallout
{
    private const int NamedAtMost = 3;

    public string Id => "powerplay-salvage";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming
            || context.State is not { } state
            || !context.Events.Any(e => e.Kind == "StartJump" && e.String("JumpType") == "Hyperspace")
            || state.Salvage.StarSystem is not { Length: > 0 } system)
        {
            yield break;
        }

        var unclaimed = state.Salvage.Unclaimed(state.Hold);

        if (unclaimed.Count == 0)
        {
            yield break;
        }

        var parts = unclaimed.Take(NamedAtMost).Select(item => $"{Words(item.Count)} {item.Name}").ToList();

        if (unclaimed.Count > NamedAtMost)
        {
            var more = unclaimed.Count - NamedAtMost;
            parts.Add($"{Words(more)} more {(more == 1 ? "kind" : "kinds")}");
        }

        var list = parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}";

        yield return new Announcement(
            "powerplay.salvage",
            $"You are leaving {system} with {list} scooped here. They only earn merits handed in at a Power contact in this system.");
    }

    private static string Words(int count) => count switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        10 => "ten",
        _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
