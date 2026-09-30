using System.Globalization;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>
/// A marker for a clue the running story owes the Commander. The app has the model write the line from the
/// hidden layer; the marker carries only the story and the clue's index, so no hidden text is queued or logged.
/// </summary>
public sealed class StoryClueCallout(NearbyFight fight) : ICallout
{
    public string Id => "story-clue";

    public const string KeyPrefix = "story.clue.";

    /// <summary>Off means no clue is spoken; a due clue waits.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Whether the Narrator speaks the clue; otherwise the core aboard does.</summary>
    public Func<bool> Narrated { get; set; } = () => false;

    /// <summary>The clue owed now, or null.</summary>
    public Func<DateTimeOffset, StoryClueDue?> Due { get; set; } = _ => null;

    /// <summary>The least time after the last unprompted line before a clue.</summary>
    public TimeSpan Quiet { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Clues queued since the app started. One that was not spoken is offered again next run.</summary>
    private readonly HashSet<string> _offered = new(StringComparer.Ordinal);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming
            || !Enabled()
            || AmbientLines.Situate(context.Status) == AmbientSituation.None
            || context.Status.Has(StatusFlags.Supercruise)
            || fight.On(context.Now, context.Status)
            || CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Quiet)
            || Due(context.Now) is not { } due)
        {
            yield break;
        }

        var key = Key(due);

        if (!_offered.Add(key))
        {
            yield break;
        }

        yield return new Announcement(key, string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Chatter = Quiet,
            Voice = Narrated() ? VoiceRole.Narrator : VoiceRole.ShipAi,
        };
    }

    public static string Key(StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return $"{KeyPrefix}{due.StoryId}.{due.Index.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>The clue a key names, or null when it is not a clue key.</summary>
    public static StoryClueDue? Parse(string? key)
    {
        if (key is null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[KeyPrefix.Length..];
        var dot = rest.LastIndexOf('.');

        return dot > 0 && int.TryParse(rest[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? new StoryClueDue(rest[..dot], index)
            : null;
    }
}
