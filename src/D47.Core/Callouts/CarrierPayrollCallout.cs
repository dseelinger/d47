using System.Globalization;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>How the name Elite has on file for the carrier's captain relates to the Commander's own name for them.</summary>
public enum CaptainNameCase
{
    /// <summary>The Commander has given the captain no name.</summary>
    Unnamed,

    /// <summary>The Commander's name is Elite's, or its first or last word.</summary>
    Same,

    /// <summary>The Commander calls the captain something else.</summary>
    Different,
}

/// <summary>The carrier captain, saying the week's payroll has gone out, once per carrier tick week (#1184).</summary>
public sealed class CarrierPayrollCallout : ICallout
{
    public string Id => "carrier-payroll";

    public const string Key = "carrier.payroll";

    /// <summary>The Commander's own name for the captain, read at every line.</summary>
    public Func<string?> CaptainName { get; set; } = () => null;

    /// <summary>The start of the tick week this was last said in, read once, and how to remember a new one.</summary>
    public Func<string?>? LastSaidWeek { get; set; }

    public Action<string>? RememberSaidWeek { get; set; }

    private string? _saidWeek;
    private bool _saidWeekLoaded;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var docked = context.Events.Any(journalEvent => journalEvent.Kind == "Docked");

        if (context.IsPriming
            || !docked
            || context.State is not { Carrier: { Owned: true, IsSquadron: false, DockedAtOwnCarrier: true } carrier } state
            || EliteCaptainName(carrier) is not { } elite)
        {
            yield break;
        }

        var week = CarrierUpkeep.TickAtOrBefore(context.Now).ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);

        if (!_saidWeekLoaded)
        {
            _saidWeek = LastSaidWeek?.Invoke();
            _saidWeekLoaded = true;
        }

        if (string.Equals(_saidWeek, week, StringComparison.Ordinal))
        {
            yield break;
        }

        _saidWeek = week;
        RememberSaidWeek?.Invoke(week);

        var owner = CommanderAddress.Said(state.Identity.Name);
        var text = Classify(elite, CaptainName()) switch
        {
            CaptainNameCase.Unnamed => $"Payroll's gone through, {owner}. Made out to me, {elite}.",
            CaptainNameCase.Same => $"Payroll's gone through, {owner}, made out to me, as it should be.",
            _ => $"Payroll's gone through. The cheque says {elite}, of course. My nom de plume.",
        };

        if (carrier.WeeklyUpkeep is { } weekly and > 0)
        {
            text += $" The carrier's upkeep for the week was {weekly:N0}.";
        }

        yield return new Announcement(Key, text)
        {
            Voice = VoiceRole.CarrierCaptain,
            Cooldown = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>The captain's name in Elite's crew list, or null when it has none.</summary>
    public static string? EliteCaptainName(CarrierState carrier)
    {
        foreach (var service in carrier.Services)
        {
            if (string.Equals(service.Role, "Captain", StringComparison.OrdinalIgnoreCase)
                && service.Name?.Trim() is { Length: > 0 } name)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Compares ignoring case and surrounding whitespace; the first or last word of Elite's name counts as the same name.</summary>
    public static CaptainNameCase Classify(string eliteName, string? setting)
    {
        var mine = setting?.Trim();

        if (string.IsNullOrEmpty(mine))
        {
            return CaptainNameCase.Unnamed;
        }

        var words = eliteName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool Is(string candidate) => string.Equals(candidate, mine, StringComparison.OrdinalIgnoreCase);

        return Is(eliteName.Trim()) || (words.Length > 0 && (Is(words[0]) || Is(words[^1])))
            ? CaptainNameCase.Same
            : CaptainNameCase.Different;
    }
}
