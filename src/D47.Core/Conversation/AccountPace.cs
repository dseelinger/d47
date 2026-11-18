namespace D47.Core.Conversation;

/// <summary>Where the current period's usage is heading.</summary>
public enum PaceOutcome
{
    /// <summary>The limit is not reached before the reset.</summary>
    Lasts,

    /// <summary>The limit is reached before the reset, and usage stops there.</summary>
    RunsOut,

    /// <summary>The limit is reached before the reset, and usage continues as overage.</summary>
    ReachesLimitThenOverage,

    /// <summary>Usage has reached a limit that stops it.</summary>
    AtLimit,

    /// <summary>Usage has reached a limit that it may go past.</summary>
    PastLimit,

    NoEstimate,
}

/// <summary>Why there is no estimate.</summary>
public enum NoEstimateReason
{
    NoResetTime,
    NothingUsed,
    FirstDay,
}

/// <summary>A forecast of one period's usage, as of the instant it was estimated.</summary>
public sealed record AccountPaceEstimate
{
    public PaceOutcome Outcome { get; init; }

    /// <summary>Set when <see cref="Outcome"/> is <see cref="PaceOutcome.NoEstimate"/>.</summary>
    public NoEstimateReason? Reason { get; init; }

    /// <summary>On <see cref="NoEstimateReason.FirstDay"/>, the instant an estimate becomes possible.</summary>
    public DateTimeOffset? EstimateFrom { get; init; }

    /// <summary>The instant the limit is reached, on <see cref="PaceOutcome.RunsOut"/> and <see cref="PaceOutcome.ReachesLimitThenOverage"/>.</summary>
    public DateTimeOffset? LimitAt { get; init; }

    /// <summary>Whole days from <see cref="LimitAt"/> to the reset, at least 1.</summary>
    public int? DaysBeforeReset { get; init; }

    /// <summary>On <see cref="PaceOutcome.Lasts"/>, the characters expected to be left at the reset.</summary>
    public long? Left { get; init; }

    /// <summary>The share of the period gone, from 0 to 1; null when there is no reset time.</summary>
    public double? PeriodGone { get; init; }
}

/// <summary>Forecasts an ElevenLabs period from the figures and the current instant.</summary>
public static class AccountPace
{
    public static AccountPaceEstimate Estimate(ElevenLabsFigures figures, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(figures);

        if (figures.NextReset is not { } reset || figures.Refresh is not { } refresh)
        {
            return NoEstimate(NoEstimateReason.NoResetTime, periodGone: null);
        }

        var start = reset.AddMonths(-(int)refresh);
        var gone = Math.Clamp((now - start).Ticks / (double)(reset - start).Ticks, 0, 1);

        var used = figures.CharactersUsed;
        var limit = figures.CharacterLimit;

        if (used >= limit)
        {
            return new AccountPaceEstimate
            {
                Outcome = figures.Extension.IsStop ? PaceOutcome.AtLimit : PaceOutcome.PastLimit,
                PeriodGone = gone,
            };
        }

        if (used <= 0)
        {
            return NoEstimate(NoEstimateReason.NothingUsed, gone);
        }

        var elapsed = now - start;

        if (elapsed < TimeSpan.FromDays(1))
        {
            return NoEstimate(NoEstimateReason.FirstDay, gone) with { EstimateFrom = start.AddDays(1) };
        }

        var toLimitTicks = (decimal)(limit - used) * elapsed.Ticks / used;

        if (toLimitTicks < (reset - now).Ticks)
        {
            var limitAt = now + TimeSpan.FromTicks((long)toLimitTicks);
            var days = (int)Math.Round((reset - limitAt).TotalDays, MidpointRounding.AwayFromZero);

            return new AccountPaceEstimate
            {
                Outcome = figures.Extension.IsStop ? PaceOutcome.RunsOut : PaceOutcome.ReachesLimitThenOverage,
                LimitAt = limitAt,
                DaysBeforeReset = Math.Max(1, days),
                PeriodGone = gone,
            };
        }

        var expected = (decimal)used * (reset - now).Ticks / elapsed.Ticks;

        return new AccountPaceEstimate
        {
            Outcome = PaceOutcome.Lasts,
            Left = (long)Math.Round(limit - used - expected, MidpointRounding.AwayFromZero),
            PeriodGone = gone,
        };
    }

    private static AccountPaceEstimate NoEstimate(NoEstimateReason reason, double? periodGone) =>
        new() { Outcome = PaceOutcome.NoEstimate, Reason = reason, PeriodGone = periodGone };
}
