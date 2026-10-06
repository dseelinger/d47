using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Twelve hours before the Powerplay cycle ends, the merits earned in it, once per cycle while pledged.</summary>
public sealed class PowerplayCycleCallout : ICallout
{
    public string Id => "powerplay-cycle";

    public const string Key = "powerplay.cycle";

    public static readonly TimeSpan Lead = TimeSpan.FromHours(12);

    /// <summary>The day and hour, UTC, the cycle turns over at.</summary>
    public Func<(DayOfWeek Day, int HourUtc)> Boundary { get; set; } = () => (DayOfWeek.Thursday, 7);

    /// <summary>The start of the cycle this was last said in, and how to remember a new one.</summary>
    public Func<string?>? LastSaidCycle { get; set; }

    public Action<string>? RememberSaidCycle { get; set; }

    private string? _sessionSaidCycle;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { Pledge.IsPledged: true } state)
        {
            yield break;
        }

        var (day, hour) = Boundary();
        var week = CommodityLedger.Week(context.Now, day, hour);
        var left = week.To - context.Now;

        if (left > Lead)
        {
            yield break;
        }

        var cycle = week.From.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);

        if (string.Equals(LastSaidCycle is { } last ? last() : _sessionSaidCycle, cycle, StringComparison.Ordinal))
        {
            yield break;
        }

        if (RememberSaidCycle is { } remember)
        {
            remember(cycle);
        }
        else
        {
            _sessionSaidCycle = cycle;
        }

        var ends = $"The Powerplay cycle ends in {Hours(left)}";
        var earned = state.CycleMerits.Since(week.From);

        var merits = earned == 1 ? "1 merit" : $"{earned.ToString("N0", CultureInfo.InvariantCulture)} merits";

        var text = earned > 0
            ? $"{ends}. You have earned {merits} for {state.Pledge.Power} this cycle."
            : $"{ends}, and you have earned no merits this cycle.";

        yield return new Announcement(Key, text) { Cooldown = TimeSpan.Zero };
    }

    /// <summary>"twelve hours", down to "less than an hour".</summary>
    private static string Hours(TimeSpan left)
    {
        var hours = (int)Math.Round(left.TotalHours, MidpointRounding.AwayFromZero);

        return hours switch
        {
            < 1 => "less than an hour",
            1 => "one hour",
            _ => $"{Words(hours)} hours",
        };
    }

    private static string Words(int count) => count switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        10 => "ten",
        11 => "eleven",
        _ => "twelve",
    };
}
