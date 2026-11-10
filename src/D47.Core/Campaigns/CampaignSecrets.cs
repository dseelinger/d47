using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Campaigns;

/// <summary>What the Nemesis arc's secret is about.</summary>
public enum SecretKind
{
    /// <summary>Someone inside the campaign faction has been passing its plans to the rival.</summary>
    Traitor,

    /// <summary>Someone other than the rival's leader is behind the rival's plans.</summary>
    Mastermind,
}

/// <summary>A reveal given: the index of the chronicle entry it was timed to, that entry's system, and when.</summary>
public sealed record RevealPoint(int Entry, string System, DateTimeOffset At);

/// <summary>The secret and its reveals as the model wrote them, before they are kept.</summary>
public sealed record SecretDraft(string Culprit, string Truth, IReadOnlyList<string> Reveals);

/// <summary>
/// The Nemesis arc's hidden layer: the rival it belongs to, what it is about, the culprit, the whole truth, the reveals in
/// order, the chronicle count when it was kept, and the reveals given so far. Spoken only a reveal at a time; never shown or
/// logged.
/// </summary>
public sealed record ArcSecret(
    string Rival,
    SecretKind Kind,
    string Culprit,
    string Truth,
    IReadOnlyList<string> Reveals,
    int From,
    IReadOnlyList<RevealPoint> Revealed);

/// <summary>The secret after a step, and the lines due.</summary>
public sealed record SecretStep(ArcSecret? Secret, IReadOnlyList<CampaignArcLine> Lines);

/// <summary>
/// The Nemesis arc's secret: written once by the model when the arc opens, checked in code, kept, and revealed a piece at a
/// time as the faction wins a conflict or sees off a coup threat. Time passing reveals nothing. The step is pure: the
/// caller supplies the time and keeps the state.
/// </summary>
public static partial class CampaignSecrets
{
    /// <summary>How many reveals a secret has. The last names the culprit.</summary>
    public const int RevealCount = 3;

    /// <summary>The longest secret or reveal kept, in characters.</summary>
    public const int MostCharacters = 300;

    /// <summary>The longest culprit's name kept, in characters.</summary>
    public const int MostCulpritCharacters = 60;

    /// <summary>The completion ceiling for one secret: a short JSON object, plus room to think.</summary>
    public const int SecretTokens = FlavourTurn.ReasoningHeadroom + 600;

    /// <summary>What the secret of the arc against <paramref name="rival"/> is about, the same on every PC with the same seed key.</summary>
    public static SecretKind KindFor(string rival, string seedKey)
    {
        ArgumentNullException.ThrowIfNull(rival);
        ArgumentNullException.ThrowIfNull(seedKey);

        return CampaignArcs.Hash($"secret|{rival.Trim()}|{seedKey.Trim()}") % 2 == 0 ? SecretKind.Traitor : SecretKind.Mastermind;
    }

    /// <summary>Whether a chronicle entry is a resolution a reveal is timed to: a conflict won, or a coup threat seen off.</summary>
    public static bool Reveals(ChronicleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return (entry.Kind == CampaignFronts.Describe(FrontKind.Conflict) && entry.Outcome == "Won")
            || (entry.Kind == CampaignFronts.Describe(FrontKind.Coup) && entry.Outcome == "Resolved");
    }

    /// <summary>
    /// The secret after one look, and the line due. The next reveal is due at the first entry that <see cref="Reveals"/>,
    /// added to the chronicle after the secret was kept and after the last reveal's entry. One reveal a look; nothing once
    /// every reveal is given, while there is no secret, or when the secret belongs to another rival than <paramref name="arc"/>'s.
    /// </summary>
    public static SecretStep Step(ArcSecret? secret, CampaignArc? arc, string faction, IReadOnlyList<ChronicleEntry> chronicle, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(chronicle);

        if (secret is null
            || arc is null
            || !string.Equals(secret.Rival, arc.Rival, StringComparison.OrdinalIgnoreCase)
            || secret.Revealed.Count >= secret.Reveals.Count)
        {
            return new SecretStep(secret, []);
        }

        var start = Math.Max(secret.From, secret.Revealed.Count == 0 ? 0 : secret.Revealed[^1].Entry + 1);

        for (var index = start; index < chronicle.Count; index++)
        {
            if (!Reveals(chronicle[index]))
            {
                continue;
            }

            var entry = chronicle[index];
            var revealed = secret with { Revealed = [.. secret.Revealed, new RevealPoint(index, entry.System, now)] };

            return new SecretStep(revealed, [Line(secret, arc, faction, entry, secret.Revealed.Count)]);
        }

        return new SecretStep(secret, []);
    }

    /// <summary>The request for the secret of <paramref name="arc"/>.</summary>
    public static string Prompt(string faction, CampaignArc arc, SecretKind kind)
    {
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(arc);

        var about = kind == SecretKind.Traitor
            ? $"someone inside {faction} has been passing its plans to {arc.Rival}. Invent that traitor"
            : $"someone other than {arc.Leader} has been directing {arc.Rival}'s plans against {faction}. Invent that hidden hand";

        return
            $"You are writing the hidden layer of a rivalry in a science-fiction game. {faction} is a minor faction. {arc.Rival} is "
            + $"a rival minor faction, led by {arc.Leader}. The secret: {about}, with a name and a role. Write the whole truth in one or "
            + $"two sentences. Then write {RevealCount} reveals, one sentence each, to be told in order, each after a victory you know "
            + "nothing about: the first a clue, the second narrowing it, the last naming the culprit. Only the last reveal may contain "
            + "the culprit's name, written exactly as you give it. Use no digits and no numbers, and name no star system, station, "
            + "planet or date. You may invent people, documents, motives and history. "
            + "Answer with JSON alone, in this shape: {\"culprit\": \"<name>\", \"secret\": \"<the whole truth>\", \"reveals\": [\"<first>\", \"<second>\", \"<last>\"]}";
    }

    /// <summary>
    /// The secret in an answer, or null when it is not the JSON asked for, or a field is missing, empty or too long, any text
    /// holds a digit, a reveal before the last names the culprit, the last does not, or any text names one of
    /// <paramref name="places"/> once <paramref name="faction"/> and <paramref name="rival"/> are set aside.
    /// </summary>
    public static SecretDraft? Read(string answer, string faction, string rival, IEnumerable<string> places)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(faction);
        ArgumentNullException.ThrowIfNull(rival);
        ArgumentNullException.ThrowIfNull(places);

        var open = answer.IndexOf('{', StringComparison.Ordinal);
        var close = answer.LastIndexOf('}');

        if (open < 0 || close <= open)
        {
            return null;
        }

        string? culprit;
        string? truth;
        List<string> reveals;

        try
        {
            using var document = JsonDocument.Parse(answer[open..(close + 1)]);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            culprit = Text(root, "culprit");
            truth = Text(root, "secret");

            if (!root.TryGetProperty("reveals", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            reveals = [.. list.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() ?? string.Empty : string.Empty)];
        }
        catch (JsonException)
        {
            return null;
        }

        if (culprit is not { Length: > 0 and <= MostCulpritCharacters }
            || truth is not { Length: > 0 and <= MostCharacters }
            || reveals.Count != RevealCount
            || reveals.Any(reveal => reveal.Length is 0 or > MostCharacters))
        {
            return null;
        }

        string[] texts = [culprit, truth, .. reveals];

        if (texts.Any(text => Digit().IsMatch(text))
            || reveals.Take(RevealCount - 1).Any(reveal => reveal.Contains(culprit, StringComparison.OrdinalIgnoreCase))
            || !reveals[^1].Contains(culprit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var named = places
            .Where(place => !string.IsNullOrWhiteSpace(place))
            .Select(place => place.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return texts.Any(text => named.Any(place => Names(SetAside(text, faction, rival), place))) ? null : new SecretDraft(culprit, truth, reveals);
    }

    /// <summary>
    /// Writes the secret of the arc under way when it has none, in one request, and keeps it while the same arc is under way
    /// and still has none. A null from <paramref name="ask"/>, or an answer <see cref="Read"/> refuses, keeps nothing.
    /// <paramref name="places"/> are the system names the secret may not name, besides the chronicle's. Returns whether one was kept.
    /// </summary>
    public static async Task<bool> RunAsync(
        CampaignStore store,
        string frontierId,
        string faction,
        string seedKey,
        IEnumerable<string> places,
        Func<string, CancellationToken, Task<string?>> ask,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(ask);

        var campaign = store.For(frontierId);

        if (campaign.Arc is not { } arc || campaign.Secret is not null)
        {
            return false;
        }

        var kind = KindFor(arc.Rival, seedKey);
        var answer = await ask(Prompt(faction, arc, kind), cancellationToken).ConfigureAwait(false);
        var draft = answer is null ? null : Read(answer, faction, arc.Rival, [.. places, .. campaign.Chronicle.Select(entry => entry.System)]);

        if (draft is null)
        {
            logger?.LogInformation("The Nemesis arc's secret was not kept: {Reason}", answer is null ? "no answer" : "the answer was refused");
            return false;
        }

        var kept = store.SetSecret(frontierId, arc, new ArcSecret(arc.Rival, kind, draft.Culprit, draft.Truth, draft.Reveals, 0, []));

        logger?.LogInformation("The Nemesis arc's secret was {Outcome}", kept ? "kept" : "not kept: the arc changed or already had one");
        return kept;
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
            maxOutputTokens: SecretTokens);

    private static CampaignArcLine Line(ArcSecret secret, CampaignArc arc, string faction, ChronicleEntry entry, int at)
    {
        var facts = new List<string>
        {
            entry.Kind == CampaignFronts.Describe(FrontKind.Conflict)
                ? $"{faction} won the conflict in {entry.System}{(entry.Opponent is { Length: > 0 } opponent ? $" against {opponent}" : string.Empty)}."
                : $"The coup threat in {entry.System} has passed.",
            $"In its aftermath this came to light: {secret.Reveals[at]}",
        };

        if (at == secret.Reveals.Count - 1)
        {
            facts.Add($"This is the last piece. The whole truth: {secret.Truth}");
        }

        return new CampaignArcLine(ArcAct.Reveal, faction, arc.Rival, CampaignLiaison.Speaker, entry.System, facts);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private static string SetAside(string text, string faction, string rival)
    {
        foreach (var name in new[] { faction, rival }.Where(name => name.Length > 0).OrderByDescending(name => name.Length))
        {
            text = text.Replace(name, " ", StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private static bool Names(string text, string place) =>
        Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(place)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\d")]
    private static partial Regex Digit();
}
