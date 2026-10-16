using System.Globalization;

namespace D47.Core.Callouts;

/// <summary>Says when the BGS has ticked, once per tick, if the tick is recent.</summary>
public sealed class BgsTickCallout : ICallout
{
    public string Id => "bgs-tick";

    public const string Key = "bgs.tick";

    /// <summary>A tick older than this is recorded and not said.</summary>
    public static readonly TimeSpan MostStale = TimeSpan.FromMinutes(60);

    /// <summary>Up to this age the line says only that the BGS has ticked.</summary>
    public static readonly TimeSpan JustNow = TimeSpan.FromMinutes(15);

    /// <summary>A tick this close to the one already said is the same tick, revised.</summary>
    public static readonly TimeSpan SameTickWithin = TimeSpan.FromHours(1);

    public Func<DateTimeOffset?> LastTick { get; set; } = () => null;

    /// <summary>The tick this was last said for, read once, and how to remember a new one.</summary>
    public Func<string?>? LastSaidTick { get; set; }

    public Action<string>? RememberSaidTick { get; set; }

    private DateTimeOffset? _said;
    private bool _saidLoaded;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || LastTick() is not { } tick)
        {
            yield break;
        }

        if (!_saidLoaded)
        {
            _said = Parse(LastSaidTick?.Invoke());
            _saidLoaded = true;
        }

        if (_said is { } said && tick - said <= SameTickWithin)
        {
            yield break;
        }

        _said = tick;
        RememberSaidTick?.Invoke(tick.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

        var age = context.Now - tick;

        if (age > MostStale)
        {
            yield break;
        }

        var text = age <= JustNow
            ? "The BGS has ticked."
            : $"The BGS ticked {Words((int)Math.Round(age.TotalMinutes / 5, MidpointRounding.AwayFromZero) * 5)} minutes ago.";

        yield return new Announcement(Key, text) { Cooldown = TimeSpan.Zero };
    }

    private static DateTimeOffset? Parse(string? stored) =>
        DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : null;

    private static string Words(int minutes) => minutes switch
    {
        15 => "fifteen",
        20 => "twenty",
        25 => "twenty-five",
        30 => "thirty",
        35 => "thirty-five",
        40 => "forty",
        45 => "forty-five",
        50 => "fifty",
        55 => "fifty-five",
        _ => "sixty",
    };
}
