using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using D47.Core.Persona;
using D47.Core.Speech;

namespace D47.Core.Stories;

/// <summary>A stock story's public layer: what the Commander picks it by.</summary>
public sealed record StoryCard
{
    /// <summary>The Save the Cat genres a card's <see cref="Genre"/> is one of.</summary>
    public static readonly IReadOnlyList<string> Genres =
    [
        "Golden Fleece",
        "Dude with a Problem",
        "Whydunit",
        "Buddy Love",
        "Rites of Passage",
        "Fool Triumphant",
        "Institutionalized",
        "Out of the Bottle",
        "Superhero",
    ];

    public required string Id { get; init; }

    public int Number { get; init; }

    public required string Title { get; init; }

    /// <summary>One of <see cref="Genres"/>.</summary>
    public required string Genre { get; init; }

    public string? Tone { get; init; }

    /// <summary>The id of the Guardian core the story is written for; it comes aboard at the beacon scan.</summary>
    public required string Core { get; init; }

    /// <summary>The name of <see cref="Core"/>.</summary>
    public string CoreName => PersonaCatalog.Resolve(Core).Name;

    /// <summary>Why a player would pick this story, like the back cover of a novel.</summary>
    public required string Blurb { get; init; }

    /// <summary>The Commander's backstory in the first person; picking the story makes it their Backstory.</summary>
    public required string InYourWords { get; init; }

    /// <summary>Why this Commander goes to a Guardian beacon.</summary>
    public string? Beacon { get; init; }

    /// <summary>The layer as the model reads it.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine(Tone is { Length: > 0 } tone ? $"{Title} — {tone}." : $"{Title}.");
        Line(text, "Genre", Genre);
        Line(text, "The core aboard once the beacon is scanned", CoreName);
        text.AppendLine($"In the Commander's words: \"{InYourWords}\"");
        Line(text, "The beacon", Beacon);
        return text.ToString().TrimEnd();
    }

    internal static void Line(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.AppendLine($"{label}: {value}");
        }
    }
}

/// <summary>One spoken line of a hidden story: what is said, and the id of the speaker who says it.</summary>
public sealed record StoryLine(string Text, string Speaker);

/// <summary>One way a story may end. <see cref="Add"/> names the persona ids that come aboard after it.</summary>
public sealed record StoryOption
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string After { get; init; }

    public IReadOnlyList<string> Add { get; init; } = [];
}

/// <summary>A speaker who exists only in one story's fiction, pinned to a local voice.</summary>
public sealed record StorySpeaker
{
    /// <summary>In every story's cast without being listed: the Commander's ship AI, in the voice they set for it.</summary>
    public const string Ship = "ship";

    /// <summary>In every story's cast without being listed: the narrator voice.</summary>
    public const string Narrator = "narrator";

    public const string Kokoro = "kokoro";

    public const string Chatterbox = "chatterbox";

    /// <summary>The Chatterbox voice recorded from the Commander.</summary>
    public const string Own = "own";

    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>One line for the writer: who they are and how they speak.</summary>
    public required string Who { get; init; }

    /// <summary><see cref="Kokoro"/> or <see cref="Chatterbox"/>.</summary>
    public required string Provider { get; init; }

    public required string Voice { get; init; }
}

/// <summary>The fifteen Save the Cat beats of one story. A line may be general; the chapter writer fills it in.</summary>
public sealed record StoryBeats
{
    public string? OpeningImage { get; init; }

    public string? ThemeStated { get; init; }

    public string? SetUp { get; init; }

    public string? Catalyst { get; init; }

    public string? Debate { get; init; }

    public string? BreakIntoTwo { get; init; }

    public string? BStory { get; init; }

    public string? FunAndGames { get; init; }

    public string? Midpoint { get; init; }

    public string? BadGuysCloseIn { get; init; }

    public string? AllIsLost { get; init; }

    public string? DarkNightOfTheSoul { get; init; }

    public string? BreakIntoThree { get; init; }

    public string? Finale { get; init; }

    public string? FinalImage { get; init; }

    /// <summary>Every beat in order, keyed as in the sealed file.</summary>
    public IReadOnlyList<(string Key, string? Line)> All =>
    [
        ("openingImage", OpeningImage),
        ("themeStated", ThemeStated),
        ("setUp", SetUp),
        ("catalyst", Catalyst),
        ("debate", Debate),
        ("breakIntoTwo", BreakIntoTwo),
        ("bStory", BStory),
        ("funAndGames", FunAndGames),
        ("midpoint", Midpoint),
        ("badGuysCloseIn", BadGuysCloseIn),
        ("allIsLost", AllIsLost),
        ("darkNightOfTheSoul", DarkNightOfTheSoul),
        ("breakIntoThree", BreakIntoThree),
        ("finale", Finale),
        ("finalImage", FinalImage),
    ];
}

/// <summary>A stock story's hidden layer. Sent to the model; never shown or logged.</summary>
public sealed record StorySecret
{
    public const int WeeklyClues = 4;

    public const int ClueCount = 14;

    public const int FinaleCount = 4;

    public const int MostOptions = 4;

    public const int MostCast = 4;

    public required string Id { get; init; }

    public required string Secret { get; init; }

    public required string End { get; init; }

    public StoryBeats Beats { get; init; } = new();

    /// <summary>Four weekly clues, then ten monthly.</summary>
    public IReadOnlyList<StoryLine> Clues { get; init; } = [];

    /// <summary>One line for each finale chapter.</summary>
    public IReadOnlyList<StoryLine> Finale { get; init; } = [];

    public IReadOnlyList<StoryOption> Options { get; init; } = [];

    /// <summary>The speakers besides <see cref="StorySpeaker.Ship"/> and <see cref="StorySpeaker.Narrator"/>.</summary>
    public IReadOnlyList<StorySpeaker> Cast { get; init; } = [];

    /// <summary>Every hidden text, named by the field it is in, for the checks that it is never shown.</summary>
    public IEnumerable<(string Field, string Text)> Texts()
    {
        yield return ("secret", Secret);
        yield return ("end", End);

        foreach (var (key, line) in Beats.All)
        {
            yield return ($"beats.{key}", line ?? string.Empty);
        }

        foreach (var (field, line) in Lines())
        {
            yield return (field, line.Text);
        }

        foreach (var option in Options)
        {
            yield return ($"options.{option.Id}.label", option.Label);
            yield return ($"options.{option.Id}.after", option.After);
        }

        foreach (var speaker in Cast)
        {
            yield return ($"cast.{speaker.Id}.name", speaker.Name);
            yield return ($"cast.{speaker.Id}.who", speaker.Who);
        }
    }

    /// <summary>Every clue and finale line, named by its place.</summary>
    public IEnumerable<(string Field, StoryLine Line)> Lines() =>
        Clues.Select((line, at) => ($"clues[{at.ToString(CultureInfo.InvariantCulture)}]", line))
            .Concat(Finale.Select((line, at) => ($"finale[{at.ToString(CultureInfo.InvariantCulture)}]", line)));
}

/// <summary>
/// The stock stories: <c>StoryCatalog.json</c> (public) and <c>StoryCatalog.sealed</c> (hidden, raw deflate then
/// base64), both embedded. <c>tools/seal-stories.py</c> decodes and re-encodes the hidden layer.
/// </summary>
public sealed class StoryCatalog
{
    public const string PublicResource = "D47.Core.StoryCatalog";

    public const string SealedResource = "D47.Core.StoryCatalog.Sealed";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<StoryCatalog> Shipped = new(() => new StoryCatalog(ReadPublic(), ReadSealed));

    private readonly Lazy<IReadOnlyDictionary<string, StorySecret>> _secrets;

    public StoryCatalog(IReadOnlyList<StoryCard> cards, Func<IReadOnlyList<StorySecret>> secrets)
    {
        Cards = cards;
        _secrets = new(() => secrets().ToDictionary(secret => secret.Id, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>The catalog built into this binary.</summary>
    public static StoryCatalog Default => Shipped.Value;

    /// <summary>Every card, in catalog order. Every card is offered.</summary>
    public IReadOnlyList<StoryCard> Cards { get; }

    public StoryCard? Find(string? id) =>
        Cards.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The hidden layer of one story, decoded on first use.</summary>
    public StorySecret? Secret(string id) => _secrets.Value.GetValueOrDefault(id);

    /// <summary>Every hidden entry, for the gate that checks each has a card.</summary>
    public IReadOnlyCollection<StorySecret> Secrets => [.. _secrets.Value.Values];

    /// <summary>
    /// Every way the catalog breaks the story format, named by story id and field. Never quotes hidden text.
    /// </summary>
    public IReadOnlyList<string> Faults()
    {
        var faults = new List<string>();
        var personas = new HashSet<string>(
            [PersonaCatalog.Covas.Id, .. PersonaCatalog.Shipped.Select(persona => persona.Id)], StringComparer.Ordinal);

        foreach (var card in Cards)
        {
            if (!StoryCard.Genres.Contains(card.Genre, StringComparer.Ordinal))
            {
                faults.Add($"{card.Id}: the genre is not one of the nine Save the Cat genres.");
            }

            if (!PersonaCatalog.IsGuardian(card.Core) || card.Core == PersonaCatalog.Heretic.Id)
            {
                faults.Add($"{card.Id}: the core is missing, is not a Guardian core, or is the Heretic.");
            }

            if (string.IsNullOrWhiteSpace(card.Blurb))
            {
                faults.Add($"{card.Id}: the blurb is empty.");
            }

            if (Secret(card.Id) is null)
            {
                faults.Add($"{card.Id}: there is no hidden entry.");
            }
        }

        foreach (var secret in Secrets)
        {
            if (Find(secret.Id) is null)
            {
                faults.Add($"{secret.Id}: the hidden entry has no card.");
            }

            faults.AddRange(Faults(secret, personas).Select(fault => $"{secret.Id}: {fault}"));
        }

        return faults;
    }

    private static IEnumerable<string> Faults(StorySecret secret, HashSet<string> personas)
    {
        if (string.IsNullOrWhiteSpace(secret.Secret))
        {
            yield return "secret is empty.";
        }

        if (string.IsNullOrWhiteSpace(secret.End))
        {
            yield return "end is empty.";
        }

        foreach (var (key, line) in secret.Beats.All.Where(beat => string.IsNullOrWhiteSpace(beat.Line)))
        {
            yield return $"beats.{key} is missing.";
        }

        if (secret.Clues.Count != StorySecret.ClueCount)
        {
            yield return $"clues has {secret.Clues.Count.ToString(CultureInfo.InvariantCulture)} lines, not {StorySecret.ClueCount.ToString(CultureInfo.InvariantCulture)}.";
        }

        if (secret.Finale.Count != StorySecret.FinaleCount)
        {
            yield return $"finale has {secret.Finale.Count.ToString(CultureInfo.InvariantCulture)} lines, not {StorySecret.FinaleCount.ToString(CultureInfo.InvariantCulture)}.";
        }

        if (secret.Options.Count is 0 or > StorySecret.MostOptions)
        {
            yield return $"options has {secret.Options.Count.ToString(CultureInfo.InvariantCulture)} entries, not 1 to {StorySecret.MostOptions.ToString(CultureInfo.InvariantCulture)}.";
        }

        foreach (var (option, at) in secret.Options.Select((option, at) => (option, at)))
        {
            var name = $"options[{at.ToString(CultureInfo.InvariantCulture)}]";

            if (string.IsNullOrWhiteSpace(option.Id) || string.IsNullOrWhiteSpace(option.Label) || string.IsNullOrWhiteSpace(option.After))
            {
                yield return $"{name} needs an id, a label and an after.";
            }

            foreach (var persona in option.Add.Where(persona => !personas.Contains(persona)))
            {
                yield return $"{name}.add names {persona}, which is not a persona.";
            }
        }

        if (secret.Cast.Count > StorySecret.MostCast)
        {
            yield return $"cast has {secret.Cast.Count.ToString(CultureInfo.InvariantCulture)} speakers, more than {StorySecret.MostCast.ToString(CultureInfo.InvariantCulture)}.";
        }

        var speakers = new HashSet<string>([StorySpeaker.Ship, StorySpeaker.Narrator], StringComparer.Ordinal);

        foreach (var (speaker, at) in secret.Cast.Select((speaker, at) => (speaker, at)))
        {
            var name = $"cast[{at.ToString(CultureInfo.InvariantCulture)}]";

            if (string.IsNullOrWhiteSpace(speaker.Id) || string.IsNullOrWhiteSpace(speaker.Name) || string.IsNullOrWhiteSpace(speaker.Who))
            {
                yield return $"{name} needs an id, a name and a who.";
            }
            else if (speakers.Contains(speaker.Id) || personas.Contains(speaker.Id))
            {
                yield return $"{name} has the id {speaker.Id}, which is taken by the ship, the narrator, a persona or another speaker.";
            }

            speakers.Add(speaker.Id ?? string.Empty);

            bool? voiceFits = speaker.Provider switch
            {
                StorySpeaker.Kokoro => KokoroAssets.VoiceIds.Contains(speaker.Voice, StringComparer.Ordinal),
                StorySpeaker.Chatterbox => !string.IsNullOrWhiteSpace(speaker.Voice),
                _ => null,
            };

            if (voiceFits is null)
            {
                yield return $"{name} has the provider {speaker.Provider}, not {StorySpeaker.Kokoro} or {StorySpeaker.Chatterbox}.";
            }
            else if (voiceFits == false)
            {
                yield return $"{name} has a voice that {speaker.Provider} does not have.";
            }
        }

        foreach (var (field, line) in secret.Lines())
        {
            if (string.IsNullOrWhiteSpace(line.Speaker))
            {
                yield return $"{field} has no speaker.";
            }
            else if (!speakers.Contains(line.Speaker))
            {
                yield return $"{field} is spoken by {line.Speaker}, who is not the ship, the narrator or in the cast.";
            }
        }
    }

    /// <summary>Decodes sealed text: base64, then raw deflate, then JSON.</summary>
    public static IReadOnlyList<StorySecret> Unseal(string sealedText)
    {
        ArgumentNullException.ThrowIfNull(sealedText);

        using var packed = new MemoryStream(Convert.FromBase64String(sealedText));
        using var deflate = new DeflateStream(packed, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<List<StorySecret>>(deflate, Json) ?? [];
    }

    private static IReadOnlyList<StoryCard> ReadPublic()
    {
        using var stream = Resource(PublicResource);
        return JsonSerializer.Deserialize<List<StoryCard>>(stream, Json) ?? [];
    }

    private static IReadOnlyList<StorySecret> ReadSealed()
    {
        using var reader = new StreamReader(Resource(SealedResource), Encoding.ASCII);
        return Unseal(reader.ReadToEnd());
    }

    private static Stream Resource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"The {name} resource is missing from the build.");
}
