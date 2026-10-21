using D47.Core.Journal;
using D47.Core.Speech;

namespace D47.Core.Callouts;

/// <summary>Says which friends have come online, as one line for a burst of arrivals.</summary>
public sealed class FriendsCallout(FriendRoster roster) : ICallout
{
    public string Id => "friends";

    public const string Key = "friends.online";

    /// <summary>How long after the first arrival the line waits for more names.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(5);

    /// <summary>How long past its time a held line is still said; a line held while the callout was off is dropped.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    private const int NamesSaid = 3;

    private readonly List<string> _held = [];

    private DateTimeOffset _due;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            _held.Clear();
            yield break;
        }

        foreach (var name in roster.Arrivals)
        {
            if (_held.Count == 0)
            {
                _due = context.Now + Hold;
            }

            if (!_held.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _held.Add(name);
            }
        }

        if (_held.Count == 0 || context.Now < _due)
        {
            yield break;
        }

        var names = _held.ToArray();
        _held.Clear();

        if (context.Now - _due <= StaleAfter)
        {
            yield return new Announcement(Key, Line(names));
        }
    }

    /// <summary>"Arcturus and Vega are online."; names past the third are a count.</summary>
    public static string Line(IReadOnlyList<string> names)
    {
        var listed = names.Take(NamesSaid).ToList();
        var others = names.Count - listed.Count;

        if (others > 0)
        {
            listed.Add(others == 1 ? "one other" : $"{SpokenNumber.UpTo999(others)} others");
        }

        var who = listed.Count == 1
            ? listed[0]
            : string.Join(", ", listed.Take(listed.Count - 1)) + " and " + listed[^1];

        return names.Count == 1 ? $"{who} is online." : $"{who} are online.";
    }
}
