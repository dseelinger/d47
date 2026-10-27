using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Every new codex entry, said as one line per tick.</summary>
public sealed class CodexDiscoveredCallout : ICallout
{
    public string Id => "codex-discovered";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        var found = context.Events.Select(CodexDiscovery.Of).OfType<CodexDiscovery>().ToList();

        if (found.Count == 0)
        {
            yield break;
        }

        var names = found.Select(discovery => discovery.Name).ToList();
        var list = names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]}";

        yield return new Announcement($"codex-discovered.{found[0].EntryId}", $"Discovered {list}.");
    }
}
