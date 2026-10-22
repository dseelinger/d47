using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>On mapping a body with biology, the variants Canonn has earlier finds of there.</summary>
public sealed class EarlierFindsCallout(ILogger<EarlierFindsCallout> logger) : ICallout
{
    private const string Biological = "$SAA_SignalType_Biological;";
    private const int Named = 3;

    // Tick thread only.
    private readonly HashSet<(long SystemAddress, string Body)> _said = [];
    private readonly HashSet<long> _asked = [];
    private readonly List<(long SystemAddress, string Body)> _waiting = [];

    public string Id => "earlier-finds";

    /// <summary>The service to ask, or null when earlier biology finds are off.</summary>
    public Func<ICommunityBiology?> Community { get; set; } = () => null;

    /// <summary>The answers so far; shared with anything else that reads them.</summary>
    public EarlierFinds Store { get; set; } = new();

    /// <summary>Starts the lookup off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var here = context.State?.Location.SystemAddress;

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var (address, body) = _waiting[i];

            if (address != here)
            {
                _waiting.RemoveAt(i);
            }
            else if (Store.TryGet(address, out var finds))
            {
                _waiting.RemoveAt(i);

                if (Announce(address, body, finds) is { } announcement)
                {
                    yield return announcement;
                }
            }
        }

        if (context.IsPriming || Community() is not { } community)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "SAASignalsFound"
                || journalEvent.Long("SystemAddress") is not { } address
                || journalEvent.String("BodyName") is not { Length: > 0 } body
                || !journalEvent.Items("Signals").Any(row =>
                    row.String("Type") == Biological && row.Int("Count") is > 0)
                || _said.Contains((address, body))
                || _waiting.Contains((address, body)))
            {
                continue;
            }

            if (Store.TryGet(address, out var known))
            {
                if (Announce(address, body, known) is { } announcement)
                {
                    yield return announcement;
                }

                continue;
            }

            _waiting.Add((address, body));

            if (_asked.Add(address) && context.State?.Location.StarSystem is { Length: > 0 } system)
            {
                Dispatch(() => LookUp(community, address, system));
            }
        }
    }

    private async Task LookUp(ICommunityBiology community, long address, string system)
    {
        try
        {
            Store.Set(address, await community.SystemFindsAsync(system, CancellationToken.None).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the earlier biology finds for system {SystemAddress}", address);
            Store.Set(address, []);
        }
    }

    private Announcement? Announce(long address, string body, IReadOnlyList<EarlierFind> finds)
    {
        if (!_said.Add((address, body)))
        {
            return null;
        }

        var variants = finds
            .Where(find => string.Equals(find.Body, body, StringComparison.OrdinalIgnoreCase))
            .GroupBy(find => find.Name)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .ToList();

        if (variants.Count == 0)
        {
            return null;
        }

        var names = variants.Take(Named).ToList();

        var text = variants.Count > Named
            ? $"{string.Join(", ", names)} and {Count(variants.Count - Named)} more"
            : names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]}";

        return new Announcement($"earlier-finds.{address}.{body}", $"Commanders have logged earlier finds on this body: {text}.");
    }

    private static string Count(int number) => number switch
    {
        1 => "one",
        2 => "two",
        _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
