using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Campaigns;

/// <summary>
/// The Legend arc: the Chronicler writes each chronicle entry once as faction history with the Commander in it, and the
/// passage is kept on the entry. Code decides every fact; the model writes the passage and may invent manner and motive.
/// </summary>
public static partial class CampaignLegend
{
    /// <summary>The longest passage kept, in characters.</summary>
    public const int MostCharacters = 700;

    /// <summary>The completion ceiling for one passage: a short paragraph, plus room to think.</summary>
    public const int PassageTokens = FlavourTurn.ReasoningHeadroom + 400;

    /// <summary>The indexes of the chronicle entries that have no passage yet.</summary>
    public static IReadOnlyList<int> Pending(IReadOnlyList<ChronicleEntry> chronicle)
    {
        ArgumentNullException.ThrowIfNull(chronicle);

        return [.. Enumerable.Range(0, chronicle.Count).Where(index => string.IsNullOrWhiteSpace(chronicle[index].Legend))];
    }

    /// <summary>The line the Chronicler writes an entry from: the entry's facts only.</summary>
    public static CampaignArcLine Line(string faction, string seedKey, ChronicleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(seedKey);
        ArgumentNullException.ThrowIfNull(entry);

        var facts = new List<string> { $"{faction} took up a {entry.Kind.ToLowerInvariant()} in {entry.System}." };

        if (entry.Opponent is { Length: > 0 } opponent)
        {
            facts.Add($"The opponent was {opponent}.");
        }

        facts.Add($"It ended: {entry.Outcome}.");

        if (entry.Ticks.Count > 0)
        {
            facts.Add($"It spanned {entry.Ticks.Count.ToString(CultureInfo.InvariantCulture)} BGS {(entry.Ticks.Count == 1 ? "tick" : "ticks")}.");
        }

        if (entry.Work.Count > 0)
        {
            facts.Add("The Commander, an independent pilot, did background simulation work for the faction while it ran.");
        }

        return new CampaignArcLine(ArcAct.Legend, faction, entry.Opponent ?? string.Empty, CampaignArcs.Chronicler(seedKey), entry.System, facts);
    }

    /// <summary>The request for one passage.</summary>
    public static string Prompt(CampaignArcLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return
            $"You are {line.Leader}, the Chronicler of {line.Faction}, a minor faction in a science-fiction game. You write its history "
            + "as a dry, vivid chronicler would. Write one passage of three or four sentences, in the past tense, about the entry "
            + "below. The Commander, an independent pilot who worked for the faction, appears in it as \"the Commander\". "
            + $"The facts: {line.Text} {CampaignArcs.FictionRule} Answer with the passage alone. Mention no game, developer or AI.";
    }

    /// <summary>
    /// The passage in an answer, or null when the answer is empty, over <see cref="MostCharacters"/>, or carries a number the
    /// facts did not give.
    /// </summary>
    public static string? Read(string answer, CampaignArcLine line)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(line);

        var passage = string.Join(' ', answer.Split('\n').Select(part => part.Trim()).Where(part => part.Length > 0)).Trim('`', '*', '"', ' ');

        if (passage.Length == 0 || passage.Length > MostCharacters)
        {
            return null;
        }

        var given = Numbers().Matches(line.Text).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);

        return Numbers().Matches(passage).Any(match => !given.Contains(match.Value)) ? null : passage;
    }

    /// <summary>
    /// Writes a passage for every entry without one, one request each, and keeps each over the entry as it was asked about.
    /// A null from <paramref name="ask"/>, or an answer <see cref="Read"/> refuses, leaves the entry for the next run.
    /// Returns how many passages were kept.
    /// </summary>
    public static async Task<int> RunAsync(
        CampaignStore store,
        string frontierId,
        string faction,
        string seedKey,
        Func<string, CancellationToken, Task<string?>> ask,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ask);

        var written = 0;

        foreach (var index in Pending(store.For(frontierId).Chronicle))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = store.For(frontierId).Chronicle[index];
            var line = Line(faction, seedKey, entry);
            var answer = await ask(Prompt(line), cancellationToken).ConfigureAwait(false);

            if (answer is null || Read(answer, line) is not { } passage)
            {
                continue;
            }

            if (store.SetLegend(frontierId, index, entry, passage))
            {
                written++;
            }
        }

        logger?.LogInformation("The Chronicler wrote {Count} chronicle passages", written);
        return written;
    }

    /// <summary>The request <see cref="RunAsync"/> makes, through the turn model.</summary>
    public static Func<string, CancellationToken, Task<string?>> Asker(
        ILlmProvider provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger) =>
        (prompt, token) => FlavourTurn.AskAsync(
            provider,
            model,
            persona: null,
            aboutMe: null,
            prompt,
            gameState: null,
            spend,
            prices,
            logger,
            token,
            maxOutputTokens: PassageTokens);

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numbers();
}
