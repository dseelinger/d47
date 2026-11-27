using System.Globalization;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// What is still worth doing in the system, said when the FSS closes. It says the same list again
/// only when <see cref="SystemWorth"/> has changed since it was last said.
/// </summary>
public sealed class FssCloseCallout(CartographyLedger? ledger, SignalSightings? signals) : ICallout
{
    private const int MostNamed = 3;

    private static readonly TimeSpan RecentlySaid = TimeSpan.FromSeconds(60);

    private static readonly string[] PerBodyKeys = ["body-value", "biology", "geology"];

    private const string UnusualKey = "unusual-signals";

    /// <summary>A gap this long between two examinations means the callout or the engine was off.</summary>
    private static readonly TimeSpan MostBetweenTicks = TimeSpan.FromSeconds(5);

    private bool _wasOpen;
    private DateTimeOffset? _lastExamined;
    private string? _lastSaid;

    public string Id => "fss-close";

    /// <summary>The least value, in credits, a planet is listed for mapping at.</summary>
    public Func<long> Threshold { get; set; } = () => 500_000;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var open = context.Status.GuiFocus == GuiFocus.FssMode;
        var seenThrough = _lastExamined is { } last && context.Now - last <= MostBetweenTicks;
        var closed = _wasOpen && seenThrough && !open;

        _lastExamined = context.Now;

        // An FSS already open at startup is not one this session saw open.
        _wasOpen = open && !context.IsPriming;

        if (!closed
            || context.IsPriming
            || context.InOperation
            || context.State is not { } state
            || state.Location.SystemAddress is not { } address)
        {
            yield break;
        }

        var answer = SystemWorth.For(
            state, address, state.Location.StarPos, ledger, Threshold(), signals?.Snapshot() ?? []);

        if (answer.Items.Count == 0)
        {
            yield break;
        }

        var signature = Signature(answer);

        if (signature == _lastSaid)
        {
            yield break;
        }

        var system = state.Location.StarSystem;
        var named = answer.Items
            .Where(item => !SaidRecently(context, address, item))
            .Take(MostNamed)
            .Select(item => Phrase(item, system))
            .ToList();

        if (named.Count == 0)
        {
            yield break;
        }

        _lastSaid = signature;

        var more = answer.Items.Count - named.Count;
        var text = "Worth doing here: " + string.Join("; ", named) + (more > 0 ? $"; and {Words(more)} more." : ".");

        if (answer.FirstDiscoveries > 0)
        {
            var count = answer.FirstDiscoveries;

            text += $" {Capitalised(Words(count))} first discover{(count == 1 ? "y" : "ies")}.";
        }

        yield return new Announcement($"fss-close.{address}", text);
    }

    private static bool SaidRecently(CalloutContext context, long address, WorthItem item)
    {
        bool Recent(string key) => context.SpokenAt(key) is { } at && context.Now - at < RecentlySaid;

        return item switch
        {
            { Kind: WorthKind.Unusual, BodyId: null } => Recent($"{UnusualKey}.{address}"),
            { BodyId: { } body } =>
                PerBodyKeys.Any(prefix => Recent($"{prefix}.{address}.{body}"))
                || (item.Kind == WorthKind.Unusual && item.Names.Any(kind => Recent($"{UnusualKey}.{address}.{body}.{kind}"))),
            _ => false,
        };
    }

    private static string Signature(SystemWorthAnswer answer) =>
        string.Join('|', answer.Items.Select(item =>
            $"{item.Kind}:{item.BodyName}:{string.Join(',', item.Names)}:{item.Low}:{item.High}:{item.FirstMapping}"))
        + $"|{answer.SystemAddress}|{answer.FirstDiscoveries}";

    internal static string Phrase(WorthItem item, string? system)
    {
        var body = BiologyCallout.Short(item.BodyName, system);

        return item.Kind switch
        {
            WorthKind.Unusual when item.BodyId is null =>
                item.BodyName == D47.Core.Knowledge.SignalKind.NotableStellarPhenomena.Name
                    ? "notable stellar phenomena"
                    : $"{item.BodyName}, notable stellar phenomena",
            WorthKind.Unusual => $"{string.Join(" and ", item.Names)} signals on {body}",
            WorthKind.Biology => $"{body} biology" + item switch
            {
                { Low: { } low, High: { } high } when low < high =>
                    $", {BiologyCallout.Credits(low)} to {BiologyCallout.Credits(high)}",
                { Low: { } low, High: { } high } when low == high => $", about {BiologyCallout.Credits(high)}",
                { High: { } high } => $", up to {BiologyCallout.Credits(high)}",
                _ => string.Empty,
            },
            WorthKind.Mapping => $"{body} to map, "
                + (item.FirstMapping ? "first mapping" : $"about {BiologyCallout.Credits(item.High ?? 0)}"),
            _ => $"{body} geology not in your codex",
        };
    }

    private static string Words(int count) => SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture));

    private static string Capitalised(string words) => char.ToUpperInvariant(words[0]) + words[1..];
}
