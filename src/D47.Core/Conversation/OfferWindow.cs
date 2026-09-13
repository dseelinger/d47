namespace D47.Core.Conversation;

/// <summary>A list of choices put to the Commander, read against their next utterance.</summary>
public sealed record Offer(IReadOnlyList<OfferChoice> Choices);

/// <summary>One choice, by the name the Commander may pick it with.</summary>
public sealed record OfferChoice(string Name, OfferTarget Target);

/// <summary>What picking a choice does.</summary>
public abstract record OfferTarget
{
    private OfferTarget()
    {
    }

    /// <summary>
    /// Routes the phrase model-free as if the Commander had said it; never handed to the model.
    /// <paramref name="Said"/> is the utterance that missed it, held so a run from a near miss can ask to
    /// learn it (#169); null for a route offered by some other means.
    /// </summary>
    public sealed record RoutePhrase(string Phrase, bool Guarded, string? Said = null) : OfferTarget;

    /// <summary>Speaks the text it returns, and opens the next offer when there is one.</summary>
    public sealed record Answer(Func<OfferAnswer> Respond) : OfferTarget;
}

public sealed record OfferAnswer(string Text, Offer? Next = null);

/// <summary>What an utterance meant to the standing offer.</summary>
public abstract record OfferReading
{
    private OfferReading()
    {
    }

    public sealed record Picked(OfferChoice Choice) : OfferReading;

    public sealed record Declined : OfferReading;

    /// <summary>An answer that fits more than one choice.</summary>
    public sealed record Unclear : OfferReading;

    /// <summary>Not an answer to the offer; the utterance routes as usual.</summary>
    public sealed record Unrelated : OfferReading;
}

/// <summary>Holds at most one offer open for one reply.</summary>
public sealed class OfferWindow
{
    private readonly Lock _gate = new();

    private Offer? _standing;
    private bool _askedWhich;

    public bool IsStanding
    {
        get
        {
            lock (_gate)
            {
                return _standing is not null;
            }
        }
    }

    /// <summary>Replaces any standing offer.</summary>
    public void Open(Offer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        lock (_gate)
        {
            _standing = offer.Choices.Count > 0 ? offer : null;
            _askedWhich = false;
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _standing = null;
            _askedWhich = false;
        }
    }

    /// <summary>
    /// Reads an utterance against the standing offer and closes it, except on the first Unclear. A second
    /// Unclear reads as Declined.
    /// </summary>
    public OfferReading Read(string utterance)
    {
        lock (_gate)
        {
            if (_standing is not { } offer)
            {
                return new OfferReading.Unrelated();
            }

            var reading = Reading(offer.Choices, utterance);

            if (reading is OfferReading.Unclear && !_askedWhich)
            {
                _askedWhich = true;
                return reading;
            }

            _standing = null;
            _askedWhich = false;

            return reading is OfferReading.Unclear ? new OfferReading.Declined() : reading;
        }
    }

    private static OfferReading Reading(IReadOnlyList<OfferChoice> choices, string utterance)
    {
        var words = Lowered(utterance);

        if (words.Length == 0)
        {
            return new OfferReading.Unrelated();
        }

        if (Ordinal(words, choices.Count) is { } position)
        {
            return position < choices.Count
                ? new OfferReading.Picked(choices[position])
                : new OfferReading.Unclear();
        }

        var said = string.Join(' ', words);

        if (choices.FirstOrDefault(choice => string.Join(' ', Lowered(choice.Name)) == said) is { } named)
        {
            return new OfferReading.Picked(named);
        }

        if (Declining.Contains(said))
        {
            return new OfferReading.Declined();
        }

        if (Affirming.Contains(said))
        {
            return choices.Count == 1
                ? new OfferReading.Picked(choices[0])
                : new OfferReading.Unclear();
        }

        var containing = choices.Where(choice => Contains(Lowered(choice.Name), words)).ToList();

        return containing.Count == 1
            ? new OfferReading.Picked(containing[0])
            : new OfferReading.Unrelated();
    }

    /// <summary>The zero-based position an ordinal names, or null when the words are not an ordinal.</summary>
    private static int? Ordinal(string[] words, int count)
    {
        var rest = words.AsSpan();

        if (rest.Length > 0 && rest[0] == "the")
        {
            rest = rest[1..];
        }

        if (rest.Length == 2 && rest[1] == "one")
        {
            rest = rest[..1];
        }

        if (rest.Length == 1)
        {
            return rest[0] == "last" ? count - 1 : Array.IndexOf(Ordinals, rest[0]) is var at and >= 0 ? at : null;
        }

        if (rest.Length == 2 && rest[0] == "number")
        {
            if (int.TryParse(rest[1], out var number) && number > 0)
            {
                return number - 1;
            }

            return Array.IndexOf(Cardinals, rest[1]) is var at and >= 0 ? at : null;
        }

        return null;
    }

    /// <summary>Whether the words appear in the name as a run of whole words.</summary>
    private static bool Contains(string[] name, string[] words)
    {
        for (var start = 0; start + words.Length <= name.Length; start++)
        {
            if (name.AsSpan(start, words.Length).SequenceEqual(words))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Lowered(string text) =>
        [.. KeywordRouter.Words(text).Select(word => word.ToLowerInvariant())];

    private static readonly string[] Ordinals = ["first", "second", "third", "fourth", "fifth"];

    private static readonly string[] Cardinals = ["one", "two", "three", "four", "five"];

    private static readonly HashSet<string> Declining =
        new(["no", "nope", "cancel", "never mind", "neither"], StringComparer.Ordinal);

    private static readonly HashSet<string> Affirming =
        new(["yes", "yeah", "do it", "go ahead", "that one"], StringComparer.Ordinal);
}
