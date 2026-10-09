using D47.Core.Conversation;

namespace D47.Core.Listening;

/// <summary>What the Commander flying has taught d47 about their words, read and written per Commander.</summary>
public sealed class LearnedWording(
    HeardNamesStore heardNames,
    LearnedPhrasesStore phrases,
    Func<string?> commander,
    Func<IReadOnlyCollection<string>> reserved,
    Func<DateTimeOffset> now)
{
    public LearnedPhrasesStore Phrases { get; } = phrases;

    public string LearnedCorrections() =>
        Flying() is not { } fid
            ? "Nothing yet. D47 learns one of these only when you correct a name it misheard."
            : heardNames.AliasesFor(fid).Summarise();

    public void ForgetCorrections()
    {
        if (Flying() is { } fid)
        {
            heardNames.ForgetCorrections(fid, now());
        }
    }

    public string HeardAsMeant(string spoken) =>
        Flying() is not { } fid ? spoken : heardNames.AliasesFor(fid).Apply(spoken);

    public void LearnCorrection(string heard, string meant)
    {
        if (Flying() is { } fid)
        {
            heardNames.Learn(
                fid,
                heard,
                meant,
                now(),
                word => reserved().Any(phrase =>
                    phrase.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public string? LearnedPhraseFor(string utterance) =>
        Flying() is { } fid ? Phrases.PhraseFor(fid, utterance) : null;

    public void LearnPhrase(string said, string phrase)
    {
        if (Flying() is { } fid)
        {
            Phrases.Learn(fid, said, phrase, now());
        }
    }

    private string? Flying() => commander() is { Length: > 0 } fid ? fid : null;
}
