using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Says who is in the wing when the Commander joins, and who joins afterwards, as one line for a burst.</summary>
public sealed class WingCallout : ICallout
{
    public string Id => "wing";

    public const string JoinKey = "wing.join";

    public const string AddKey = "wing.add";

    /// <summary>How long after the first <c>WingAdd</c> the line waits for more names.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(5);

    /// <summary>How long after <c>LoadGame</c> a <c>WingAdd</c> restores the wing rather than adding to it.</summary>
    public static readonly TimeSpan RestoreWindow = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    private readonly List<string> _held = [];

    private DateTimeOffset _due;

    private DateTimeOffset? _loadedAt;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var lines = new List<Announcement>();

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _loadedAt = journalEvent.Timestamp;
                    _held.Clear();
                    break;

                case "WingJoin" when !context.IsPriming:
                    _held.Clear();
                    var others = journalEvent.Items("Others")
                        .Select(other => other.ValueKind == System.Text.Json.JsonValueKind.String ? other.GetString() : null)
                        .OfType<string>()
                        .ToList();

                    if (others.Count > 0)
                    {
                        lines.Add(new Announcement(JoinKey, $"We're in a wing with {FriendsCallout.Listed(others)}."));
                    }

                    break;

                case "WingAdd" when !context.IsPriming && journalEvent.String("Name") is { Length: > 0 } name && !Restoring(journalEvent):
                    if (_held.Count == 0)
                    {
                        _due = context.Now + Hold;
                    }

                    if (!_held.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        _held.Add(name);
                    }

                    break;
            }
        }

        if (context.IsPriming)
        {
            _held.Clear();
        }

        foreach (var line in lines)
        {
            yield return line;
        }

        if (_held.Count == 0 || context.Now < _due)
        {
            yield break;
        }

        var names = _held.ToArray();
        _held.Clear();

        if (context.Now - _due <= StaleAfter)
        {
            yield return new Announcement(AddKey, $"{FriendsCallout.Listed(names)} joined the wing.");
        }
    }

    private bool Restoring(JournalEvent journalEvent) =>
        _loadedAt is { } loaded && journalEvent.Timestamp - loaded is { } gap && gap >= TimeSpan.Zero && gap <= RestoreWindow;
}
