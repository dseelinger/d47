using D47.Core.Audio;

namespace D47.Core.Capabilities;

/// <summary>What a voice row says of its Chatterbox clip: a state word, the clip's size, and whether the clip is on this PC.</summary>
/// <param name="State">The state word and its tone.</param>
/// <param name="Size">The clip's size, <c>247 KB</c>, or null where the state states none.</param>
/// <param name="OnThisPc">Whether the voice plays without a fetch.</param>
/// <param name="CanRetry">Whether the row offers a Retry that fetches again.</param>
public sealed record ChoiceClip(ChoiceStatus State, string? Size, bool OnThisPc, bool CanRetry)
{
    /// <summary>Where the clip is.</summary>
    public ChatterboxClip Where { get; init; }

    /// <summary>Why the last fetch failed, as the log states it; null unless it failed.</summary>
    public string? Reason { get; init; }

    /// <summary>The row's clip line for one clip state.</summary>
    public static ChoiceClip Of(ChatterboxClipState clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var size = SizeOf(clip.Bytes);

        var line = clip.Clip switch
        {
            ChatterboxClip.Shipped => new ChoiceClip(new ChoiceStatus("Shipped", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Fetched => new ChoiceClip(new ChoiceStatus("Fetched", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Custom => new ChoiceClip(new ChoiceStatus("Custom", ChoiceTone.Yellow), null, true, false),
            ChatterboxClip.Fetching => new ChoiceClip(new ChoiceStatus("Fetching", ChoiceTone.White), size, false, false),
            ChatterboxClip.Failed => new ChoiceClip(new ChoiceStatus("Fetch failed", ChoiceTone.Red), size, false, true),
            _ => new ChoiceClip(new ChoiceStatus("Not fetched", ChoiceTone.Grey), size, false, false),
        };

        return line with { Where = clip.Clip, Reason = clip.Reason };
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

    /// <summary>One voice's name alone, <c>Lucie</c>, or null where the voice is not listed.</summary>
    public Func<string, string?>? Name { get; init; }

    /// <summary>The name of the shipped voice that speaks for one voice while its clip is not here, or null where none can.</summary>
    public Func<string, string?>? StandIn { get; init; }

    /// <summary>Whether Chatterbox's model is downloaded; null for yes.</summary>
    public Func<bool>? Installed { get; init; }

    /// <summary>The line under a voice row while <paramref name="id"/> is not on this PC, or null while it is.</summary>
    public ChoiceAway? Away(string id)
    {
        if (Clip(id) is not { OnThisPc: false } clip)
        {
            return null;
        }

        var name = NameOf(id);
        var host = ChatterboxCatalog.Host;

        return clip.Where switch
        {
            ChatterboxClip.Fetching => new ChoiceAway(clip.Where, Meanwhile(id, standIn => $"Until {name} arrives, D47 speaks in {standIn}."))
            {
                Head = "FETCHING",
                Mono = string.Join(" · ", new[] { clip.Size, host.ToUpperInvariant() }.OfType<string>()),
            },
            ChatterboxClip.Failed => new ChoiceAway(
                clip.Where,
                Sentences(Meanwhile(id, standIn => $"D47 speaks in {standIn} until it arrives."), $"Retry asks {host} again."))
            {
                Label = $"{name} could not be fetched",
                Reason = clip.Reason,
            },
            _ => new ChoiceAway(
                clip.Where,
                Meanwhile(id, standIn => $"D47 fetches {name} before its next line, and speaks in {standIn} if that takes more than {Seconds} seconds."))
            {
                Head = "NOT ON THIS PC",
                Mono = clip.Size,
            },
        };
    }

    /// <summary>What choosing <paramref name="id"/> would do, said under the picker's list, or null for a voice not listed.</summary>
    public string? Choosing(string id)
    {
        if (Clip(id) is not { } clip)
        {
            return null;
        }

        var name = NameOf(id);

        if (clip.OnThisPc)
        {
            return $"{name} is on this PC.";
        }

        var host = ChatterboxCatalog.Host;

        return clip.Where switch
        {
            ChatterboxClip.Fetching => Sentences(
                $"{name} is arriving from {host}.",
                Meanwhile(id, standIn => $"Until it does, D47 speaks in {standIn}.")),
            ChatterboxClip.Failed => Sentences(
                $"The last fetch of {name} failed. Choosing it tries {host} again.",
                Meanwhile(id, standIn => $"Until it arrives, D47 speaks in {standIn}.")),
            _ => Sentences(
                clip.Size is { } size ? $"Choosing {name} fetches {size} from {host}." : $"Choosing {name} fetches it from {host}.",
                Meanwhile(id, standIn => $"Until it arrives, D47 speaks in {standIn}.")),
        };
    }

    /// <summary>
    /// Who speaks while <paramref name="id"/> is away: <paramref name="named"/> given the stand-in, or the sentence
    /// that none can stand in. Null before the model is downloaded, when no Chatterbox voice speaks at all.
    /// </summary>
    private string? Meanwhile(string id, Func<string, string> named) =>
        Installed?.Invoke() == false
            ? null
            : StandIn?.Invoke(id) is { } standIn ? named(standIn) : ChatterboxCatalog.NoStandIn(NameOf(id));

    private static string? Sentences(params string?[] sentences) =>
        sentences.OfType<string>().ToArray() is { Length: > 0 } said ? string.Join(" ", said) : null;

    private static string Seconds =>
        ChatterboxCatalog.FirstLineWait.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private string NameOf(string id) => Name?.Invoke(id) ?? id;
}

/// <summary>The line under a voice row whose chosen voice is not on this PC.</summary>
/// <param name="Clip">Where the clip is: fetching, not fetched or failed.</param>
/// <param name="Text">Who speaks meanwhile; null before the model is downloaded.</param>
public sealed record ChoiceAway(ChatterboxClip Clip, string? Text)
{
    /// <summary>The state, <c>FETCHING</c> or <c>NOT ON THIS PC</c>; null on a failure, which is a notice.</summary>
    public string? Head { get; init; }

    /// <summary>The mono size and host, <c>247 KB · GITHUB.COM</c>, or null.</summary>
    public string? Mono { get; init; }

    /// <summary>The notice's label on a failure, <c>Lucie could not be fetched</c>.</summary>
    public string? Label { get; init; }

    /// <summary>Why the fetch failed, for the notice's detail line.</summary>
    public string? Reason { get; init; }
}
