using D47.Core.Audio;

namespace D47.Core.Capabilities;

/// <summary>What a voice row says of its Chatterbox clip: a state word, the clip's size, and whether the clip is on this PC.</summary>
/// <param name="State">The state word and its tone.</param>
/// <param name="Size">The clip's size, <c>247 KB</c>, or null where the state states none.</param>
/// <param name="OnThisPc">Whether the voice plays without a fetch.</param>
/// <param name="CanRetry">Whether the row offers a Retry that fetches again.</param>
public sealed record ChoiceClip(ChoiceStatus State, string? Size, bool OnThisPc, bool CanRetry)
{
    /// <summary>The row's clip line for one clip state.</summary>
    public static ChoiceClip Of(ChatterboxClipState clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var size = SizeOf(clip.Bytes);

        return clip.Clip switch
        {
            ChatterboxClip.Shipped => new ChoiceClip(new ChoiceStatus("Shipped", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Fetched => new ChoiceClip(new ChoiceStatus("Fetched", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Custom => new ChoiceClip(new ChoiceStatus("Custom", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Fetching => new ChoiceClip(new ChoiceStatus("Fetching", ChoiceTone.White), size, false, false),
            ChatterboxClip.Failed => new ChoiceClip(new ChoiceStatus("Fetch failed", ChoiceTone.Red), size, false, true),
            _ => new ChoiceClip(new ChoiceStatus("Not fetched", ChoiceTone.Grey), size, false, false),
        };
    }

    /// <summary>A byte count in kilobytes, or null for none.</summary>
    public static string? SizeOf(long bytes) =>
        bytes <= 0 ? null : $"{Math.Max(1, (bytes + 512) / 1024)} KB";
}

/// <summary>What a Chatterbox voice picker adds to each row: a detail line, the clip state, and a way to fetch a clip.</summary>
public sealed record SettingChoiceClips
{
    /// <summary>The clip line of one voice, or null where the voice is not listed.</summary>
    public required Func<string, ChoiceClip?> Clip { get; init; }

    /// <summary>The second line of one voice, <c>Female · mid pitch · even pace</c>, or null.</summary>
    public Func<string, string?>? Detail { get; init; }

    /// <summary>Subscribes a refresh, given the voice id, to clip changes, returning the unsubscribe. Raised on any thread.</summary>
    public Func<Action<string>, Action>? Watch { get; init; }

    /// <summary>Fetches one voice's clip without playing it. Never waits.</summary>
    public Action<string>? Fetch { get; init; }

    /// <summary>The shipped voice suggested for the role the picker opened for, with the role's name.</summary>
    public (string Voice, string Role)? Suggested { get; init; }

    /// <summary>The help line above the list.</summary>
    public string Help { get; init; } = $"A voice not on this PC is fetched from {ChatterboxCatalog.Host} first.";
}
