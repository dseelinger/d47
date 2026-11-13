using System.Globalization;
using D47.Core.Audio;
using D47.Core.Input;

namespace D47.Core.Callouts;

/// <summary>
/// Says how many of d47's keys and buttons Elite binds too: once when the conflicts are first known, then once for
/// each conflict that appears later. A conflict is said at most once a run.
/// </summary>
/// <param name="conflicts">The certain conflicts, or null until Elite's bindings and the controllers have been read.</param>
public sealed class BindingConflictCallout(Func<IReadOnlyList<BindingConflict>?> conflicts) : ICallout
{
    public const string CalloutId = "binding-conflicts";

    public const string Key = "binds.conflicts";

    private readonly HashSet<string> _spoken = new(StringComparer.Ordinal);

    private bool _judged;

    public string Id => CalloutId;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.InOperation || conflicts() is not { } now)
        {
            yield break;
        }

        var fresh = now
            .GroupBy(Identity, StringComparer.Ordinal)
            .Where(same => !_spoken.Contains(same.Key))
            .Select(same => same.First())
            .ToList();

        var first = !_judged;
        _judged = true;

        if (fresh.Count == 0)
        {
            yield break;
        }

        foreach (var conflict in fresh)
        {
            _spoken.Add(Identity(conflict));
        }

        yield return new Announcement(Key, first ? StartLine(fresh) : LaterLine(fresh));
    }

    /// <summary>"Commander, Elite also binds four of my keys and buttons. They are on the Binding conflicts page."</summary>
    public static string StartLine(IReadOnlyList<BindingConflict> conflicts) =>
        $"Commander, Elite also binds {Words(conflicts.Count)} of my {Nouns(conflicts)}. {Where(conflicts.Count)}";

    /// <summary>"Elite now binds one more of my buttons. It is on the Binding conflicts page."</summary>
    public static string LaterLine(IReadOnlyList<BindingConflict> conflicts) =>
        $"Elite now binds {Words(conflicts.Count)} more of my {Nouns(conflicts)}. {Where(conflicts.Count)}";

    /// <summary>The control, the press and Elite's actions: a rebind to another conflicting press is another conflict.</summary>
    private static string Identity(BindingConflict conflict) =>
        string.Join(
            '\u001f',
            conflict.Kind,
            conflict.Control,
            conflict.RowKey,
            conflict.Switch,
            conflict.Position,
            conflict.Gesture,
            string.Join('\u001e', conflict.EliteActions));

    private static string Words(int count) => SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture));

    private static string Nouns(IReadOnlyList<BindingConflict> conflicts)
    {
        var keys = conflicts.Any(conflict => conflict.Kind == BindingKind.Key);
        var buttons = conflicts.Any(conflict => conflict.Kind != BindingKind.Key);

        return keys && buttons ? "keys and buttons" : keys ? "keys" : "buttons";
    }

    private static string Where(int count) =>
        count == 1 ? "It is on the Binding conflicts page." : "They are on the Binding conflicts page.";
}
