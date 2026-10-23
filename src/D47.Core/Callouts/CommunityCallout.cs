using System.Globalization;
using D47.Core.Interface;

namespace D47.Core.Callouts;

/// <summary>One line pointing at the D47 Discord, at most once every <see cref="Every"/> and once per launch.</summary>
public sealed class CommunityCallout : ICallout
{
    public string Id => "community";

    public const string Text = "If you have a question or an idea for me, the D47 Discord is where the other Commanders are.";

    public TimeSpan Every { get; set; } = TimeSpan.FromDays(14);

    /// <summary>How long after the session's first live tick before the line may be said.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long a quiet situation has to hold before the line may be said.</summary>
    public TimeSpan Hold { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>The local day (<c>yyyy-MM-dd</c>) the line was last queued, and how to remember a new one.</summary>
    public Func<string?>? LastSaidDay { get; set; }

    public Action<string>? RememberSaidDay { get; set; }

    private DateTimeOffset? _firstLive;
    private AmbientSituation _situation = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private bool _done;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        _firstLive ??= context.Now;

        var situation = AmbientLines.Situate(context.Status);
        if (situation != _situation)
        {
            _situation = situation;
            _situationSince = context.Now;
        }

        if (_done)
        {
            yield break;
        }

        var today = context.Now.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!DateOnly.TryParseExact(LastSaidDay?.Invoke(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
        {
            RememberSaidDay?.Invoke(today);
            _done = true;
            yield break;
        }

        var due = DateOnly.FromDateTime(context.Now.LocalDateTime).DayNumber - last.DayNumber >= Every.TotalDays;

        if (!due
            || context.Now - _firstLive < Settle
            || situation is not (AmbientSituation.Docked or AmbientSituation.Landed or AmbientSituation.Supercruise)
            || context.Now - _situationSince < Hold)
        {
            yield break;
        }

        _done = true;
        RememberSaidDay?.Invoke(today);

        yield return new Announcement(Community.SourceKey, Text)
        {
            Urgency = CalloutUrgency.Routine,
        };
    }
}
