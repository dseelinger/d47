using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>A cast reaction the running story may give: the story, and the play session it is given in.</summary>
public sealed record StoryReactionDue(string StoryId, int Session);

/// <summary>
/// The running story while it takes reactions: who is playing it, the session it is in, and whether a reaction was
/// spoken in that session.
/// </summary>
public sealed record StoryReactionWindow(string? Commander, string StoryId, int Session, bool Reacted)
{
    public StoryReactionDue Due => new(StoryId, Session);
}

/// <summary>
/// A marker for one primary cast member's line about the newest notable thing the Commander did, at most one a play
/// session, said once they are docked. The marker carries only the story and the session, so no event or hidden text
/// is queued or logged.
/// </summary>
public sealed class StoryReactionCallout(NearbyFight fight) : ICallout
{
    public string Id => "story-reaction";

    public const string KeyPrefix = "story.reaction.";

    /// <summary>Between a spoken reaction's session and the id of the member who spoke it, which may contain dots or digits.</summary>
    private const char SpokenBy = '/';

    /// <summary>Off means no reaction is spoken; what the Commander does is still noted.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>The running story's window, or null while nothing the Commander does is noted for it.</summary>
    public Func<StoryReactionWindow?> Window { get; set; } = () => null;

    /// <summary>The least time after the last unprompted line before a reaction.</summary>
    public TimeSpan Quiet { get; set; } = TimeSpan.FromMinutes(2);

    private readonly Lock _gate = new();

    /// <summary>Reactions queued since the app started. One that was not spoken is offered again next session.</summary>
    private readonly HashSet<string> _offered = new(StringComparer.Ordinal);

    private (string? Commander, string StoryId)? _noting;

    private IReadOnlyList<Notable> _notables = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (Window() is not { } window)
        {
            yield break;
        }

        // Noted before any hold, so what happened on a tick the Commander was flying is not lost.
        if (!context.IsPriming)
        {
            Note(window, context.Events);
        }

        if (context.IsPriming
            || window.Reacted
            || !Enabled()
            || AmbientLines.Situate(context.Status) != AmbientSituation.Docked
            || fight.On(context.Now, context.Status)
            || context.InOperation
            || CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Quiet)
            || Newest(window.Commander, window.Due) is null
            || !Offer(Key(window.Due)))
        {
            yield break;
        }

        yield return new Announcement(Key(window.Due), string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Chatter = Quiet,
            Voice = VoiceRole.Crew,
        };
    }

    /// <summary>The newest settled notable event since the last reaction, or null when there is none for this story.</summary>
    public Notable? Newest(string? commander, StoryReactionDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        lock (_gate)
        {
            return IsNoting(commander, due.StoryId) ? ChapterNotables.Told(_notables).FirstOrDefault() : null;
        }
    }

    /// <summary>Forgets what was noted for the story, once a reaction to it has been spoken.</summary>
    public void Reacted(string? commander, StoryReactionDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        lock (_gate)
        {
            if (IsNoting(commander, due.StoryId))
            {
                _notables = [];
            }
        }
    }

    public static string Key(StoryReactionDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return $"{KeyPrefix}{due.StoryId}.{due.Session.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>The key of a reaction spoken by the cast member <paramref name="castId"/>.</summary>
    public static string Key(StoryReactionDue due, string castId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(castId);

        return $"{Key(due)}{SpokenBy}{castId}";
    }

    /// <summary>The reaction a key names, with the cast member who spoke it when the key carries one; null when it is not a reaction key.</summary>
    public static (StoryReactionDue Due, string? CastId)? Parse(string? key)
    {
        if (key is null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[KeyPrefix.Length..];
        var by = rest.IndexOf(SpokenBy, StringComparison.Ordinal);

        if (by < 0)
        {
            return Split(rest) is { } marker ? (marker, null) : null;
        }

        return Split(rest[..by]) is { } spoken && rest[(by + 1)..] is { Length: > 0 } castId
            ? (spoken, castId)
            : null;
    }

    private static StoryReactionDue? Split(string rest)
    {
        var dot = rest.LastIndexOf('.');

        return dot > 0 && int.TryParse(rest[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var session)
            ? new StoryReactionDue(rest[..dot], session)
            : null;
    }

    private void Note(StoryReactionWindow window, IReadOnlyList<JournalEvent> events)
    {
        lock (_gate)
        {
            if (!IsNoting(window.Commander, window.StoryId))
            {
                _noting = (window.Commander, window.StoryId);
                _notables = [];
            }

            foreach (var journalEvent in events)
            {
                _notables = ChapterNotables.Fold(_notables, journalEvent);
            }
        }
    }

    private bool Offer(string key)
    {
        lock (_gate)
        {
            return _offered.Add(key);
        }
    }

    private bool IsNoting(string? commander, string storyId) =>
        _noting is var (noted, story)
        && string.Equals(noted, commander, StringComparison.Ordinal)
        && string.Equals(story, storyId, StringComparison.OrdinalIgnoreCase);
}
