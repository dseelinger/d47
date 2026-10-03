using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Persona;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>A stock story's public layer: what the Commander picks it by.</summary>
public sealed record StoryCard
{
    /// <summary>The ten Save the Cat genres a card's <see cref="Genre"/> is one of.</summary>
    public static readonly IReadOnlyList<string> Genres =
    [
        "Monster in the House",
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

    /// <summary>The Commander levels a card's <see cref="Level"/> is one of; a guide to choosing, not a limit.</summary>
    public static readonly IReadOnlyList<string> Levels = ["new", "midrange", "endgame"];

    public required string Id { get; init; }

    public int Number { get; init; }

    public required string Title { get; init; }

    /// <summary>One of <see cref="Genres"/>.</summary>
    public required string Genre { get; init; }

    /// <summary>One of <see cref="Levels"/>: the Commander the story was written for.</summary>
    public required string Level { get; init; }

    public string? Tone { get; init; }

    /// <summary>The key of one of <see cref="StoryPacing.All"/>: how long the story runs.</summary>
    public string? Length { get; init; }

    /// <summary>How the story is paced: by its <see cref="Length"/>, or as a year for a missing or unknown key.</summary>
    public StoryPacing Pacing => StoryPacing.Find(Length) ?? StoryPacing.OneYear;

    /// <summary>The id of the Guardian core the story is written for; it comes aboard at the beacon scan.</summary>
    public required string Core { get; init; }

    /// <summary>The level as the list row names it.</summary>
    public string LevelName => Level switch
    {
        "new" => "New commander",
        "midrange" => "Mid-range commander",
        "endgame" => "Endgame commander",
        _ => Level,
    };

    /// <summary>What the level means, as the story's page shows it.</summary>
    public string LevelGuideline => Level switch
    {
        "new" => "For a new commander: no engineering done yet.",
        "midrange" => "For a mid-range commander: some ship engineering done.",
        "endgame" => "For an endgame commander: most ship and on-foot engineers unlocked, and at least one ship, suit and weapon fully engineered.",
        _ => Level,
    };

    /// <summary>The name of <see cref="Core"/>.</summary>
    public string CoreName => PersonaCatalog.Resolve(Core).Name;

    /// <summary>Why a player would pick this story, like the back cover of a novel.</summary>
    public required string Blurb { get; init; }

    /// <summary>The Commander's backstory in the first person; picking the story makes it their Backstory.</summary>
    public required string InYourWords { get; init; }

    /// <summary>Why this Commander goes to a Guardian beacon.</summary>
    public string? Beacon { get; init; }

    /// <summary>The picture names of the primary cast, in cast order, both version names for a member with versions.</summary>
    public IReadOnlyList<string> CastPictures { get; init; } = [];

    /// <summary>
    /// The names in <see cref="CastPictures"/> a Commander of <paramref name="gender"/> sees: each name without a
    /// version suffix, and for a member with versions the one for the gender, or the other when it is missing. A
    /// member with versions shows nothing while the gender is unset.
    /// </summary>
    public IReadOnlyList<string> PicturesFor(string? gender)
    {
        var shown = new List<string>();
        var versions = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var name in CastPictures)
        {
            if (VersionOf(name) is not { } split)
            {
                order.Add(name);
                continue;
            }

            if (!versions.TryGetValue(split.Base, out var keyed))
            {
                versions[split.Base] = keyed = new Dictionary<string, string>(StringComparer.Ordinal);
                order.Add(split.Base);
            }

            keyed[split.Key] = name;
        }

        foreach (var entry in order)
        {
            if (!versions.TryGetValue(entry, out var keyed))
            {
                shown.Add(entry);
                continue;
            }

            if (!CommanderGender.IsSet(gender))
            {
                continue;
            }

            var woman = gender == CommanderGender.Woman;
            var first = woman ? StorySpeakerVersions.ForWomanKey : StorySpeakerVersions.ForManKey;
            var second = woman ? StorySpeakerVersions.ForManKey : StorySpeakerVersions.ForWomanKey;

            if (keyed.TryGetValue(first, out var name) || keyed.TryGetValue(second, out name))
            {
                shown.Add(name);
            }
        }

        return shown;
    }

    private static (string Base, string Key)? VersionOf(string name)
    {
        foreach (var key in new[] { StorySpeakerVersions.ForManKey, StorySpeakerVersions.ForWomanKey })
        {
            if (name.EndsWith("." + key, StringComparison.Ordinal))
            {
                return (name[..^(key.Length + 1)], key);
            }
        }

        return null;
    }

    /// <summary>The layer as the model reads it.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine(Tone is { Length: > 0 } tone ? $"{Title} — {tone}." : $"{Title}.");
        Line(text, "Genre", Genre);
        Line(text, "Length", Pacing.Name);
        Line(text, "Written for", LevelGuideline);
        Line(text, "The core aboard once the beacon is scanned", CoreName);
        text.AppendLine($"In the Commander's words: \"{InYourWords}\"");
        Line(text, "The beacon", Beacon);
        return text.ToString().TrimEnd();
    }

    /// <summary>The most characters of the Commander's words an <see cref="Excerpt"/> carries, beyond the first sentence.</summary>
    public const int ExcerptWords = 240;

    /// <summary>A short excerpt of the layer: the title and tone, and the Commander's words cut to whole sentences.</summary>
    public string Excerpt()
    {
        var sentences = Regex.Split(InYourWords.Trim(), @"(?<=[.!?])\s+");
        var words = sentences[0];

        foreach (var sentence in sentences.Skip(1))
        {
            if (words.Length + 1 + sentence.Length > ExcerptWords)
            {
                break;
            }

            words += " " + sentence;
        }

        var heading = Tone is { Length: > 0 } tone ? $"\"{Title}\" — {tone}." : $"\"{Title}\".";

        return $"{heading} In the Commander's words: \"{words}\"";
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

    /// <summary>Null when the member has <see cref="Versions"/>.</summary>
    public string? Name { get; init; }

    /// <summary>One line for the writer: who they are and how they speak.</summary>
    public required string Who { get; init; }

    /// <summary><see cref="Kokoro"/> or <see cref="Chatterbox"/>.</summary>
    public required string Provider { get; init; }

    /// <summary>Null when the member has <see cref="Versions"/>.</summary>
    public string? Voice { get; init; }

    /// <summary>The member as a Commander who is a man, and one who is a woman, meets them; null for one version.</summary>
    public StorySpeakerVersions? Versions { get; init; }

    /// <summary>A recurring character the Commander knows from the card. A primary member always has a picture.</summary>
    public bool Primary { get; init; }

    /// <summary>Signal strength, 0 to 1, of the comms link the member is heard through; null for none.</summary>
    public double? Link { get; init; }

    /// <summary>Guardian effects the member's voice passes through, in order.</summary>
    public IReadOnlyList<StorySpeakerEffect>? Effects { get; init; }

    /// <summary>
    /// The member as a Commander of <paramref name="gender"/> meets them. An unset gender meets the
    /// <see cref="StorySpeakerVersions.ForMan"/> version.
    /// </summary>
    public StorySpeakerShown Shown(string storyId, string? gender)
    {
        var woman = gender == CommanderGender.Woman;

        if (Versions is not { } versions || (woman ? versions.ForWoman ?? versions.ForMan : versions.ForMan ?? versions.ForWoman) is not { } version)
        {
            return new StorySpeakerShown(Id, Name ?? string.Empty, Provider, Voice ?? string.Empty, $"{storyId}.{Id}") { Primary = Primary };
        }

        var key = woman && versions.ForWoman is not null ? StorySpeakerVersions.ForWomanKey : StorySpeakerVersions.ForManKey;
        return new StorySpeakerShown(Id, version.Name, version.Provider ?? Provider, version.Voice, $"{storyId}.{Id}.{key}") { Primary = Primary };
    }
}

/// <summary>What the Commander has said of their Commander, for a story with a member in two versions.</summary>
public static class CommanderGender
{
    public const string Man = "man";

    public const string Woman = "woman";

    /// <summary>Whether <paramref name="gender"/> is <see cref="Man"/> or <see cref="Woman"/>.</summary>
    public static bool IsSet(string? gender) => gender is Man or Woman;
}

/// <summary>One version of a cast member.</summary>
public sealed record StorySpeakerVersion
{
    public required string Name { get; init; }

    /// <summary>Null for the member's own provider.</summary>
    public string? Provider { get; init; }

    public required string Voice { get; init; }
}

/// <summary>One Guardian effect on a cast member's voice: a <see cref="Audio.GuardianEffect.Id"/> and a level, 1 to 20.</summary>
public sealed record StorySpeakerEffect(string Id, int Level);

/// <summary>A cast member's two versions, keyed by the Commander who meets them, never by the character.</summary>
public sealed record StorySpeakerVersions
{
    /// <summary>The picture suffix of <see cref="ForMan"/>.</summary>
    public const string ForManKey = "for-man";

    /// <summary>The picture suffix of <see cref="ForWoman"/>.</summary>
    public const string ForWomanKey = "for-woman";

    /// <summary>The version a Commander who is a man meets.</summary>
    public StorySpeakerVersion? ForMan { get; init; }

    /// <summary>The version a Commander who is a woman meets.</summary>
    public StorySpeakerVersion? ForWoman { get; init; }

    /// <summary>Both versions with their picture suffixes, leaving out one that is missing.</summary>
    public IEnumerable<(string Key, StorySpeakerVersion Version)> All()
    {
        if (ForMan is { } man)
        {
            yield return (ForManKey, man);
        }

        if (ForWoman is { } woman)
        {
            yield return (ForWomanKey, woman);
        }
    }
}

/// <summary>
/// A cast member as the current Commander meets them. <see cref="Picture"/> names <c>&lt;Picture&gt;.jpg</c> in
/// <see cref="AppPaths.Stories"/>.
/// </summary>
public sealed record StorySpeakerShown(string Id, string Name, string Provider, string Voice, string Picture)
{
    /// <summary>Whether the member is primary, so has a picture.</summary>
    public bool Primary { get; init; }
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
public sealed partial record StorySecret
{
    public const int MostOptions = 4;

    public const int MostCast = 4;

    public required string Id { get; init; }

    public required string Secret { get; init; }

    public required string End { get; init; }

    public StoryBeats Beats { get; init; } = new();

    /// <summary>The Commander's beacon scan before the story opens, narrated at the pick; only a story whose length narrates the scan has one.</summary>
    public StoryLine? Scan { get; init; }

    /// <summary>One line for each clue day of the card's length.</summary>
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
            yield return ($"cast.{speaker.Id}.name", speaker.Name ?? string.Empty);

            foreach (var (key, version) in speaker.Versions?.All() ?? [])
            {
                yield return ($"cast.{speaker.Id}.{key}.name", version.Name);
            }

            yield return ($"cast.{speaker.Id}.who", speaker.Who);
        }
    }

    /// <summary>The cast member <paramref name="castId"/> as a Commander of <paramref name="gender"/> meets them, or null.</summary>
    public StorySpeakerShown? Speaker(string castId, string? gender) =>
        Cast.FirstOrDefault(speaker => string.Equals(speaker.Id, castId, StringComparison.Ordinal))?.Shown(Id, gender);

    /// <summary>
    /// This hidden layer with every <c>{name:&lt;cast-id&gt;}</c> replaced by the name a Commander of
    /// <paramref name="gender"/> meets. Resolve before any hidden text is shown, spoken, logged or written from.
    /// </summary>
    public StorySecret For(string? gender)
    {
        string Resolve(string text) => NameToken().Replace(
            text, match => Speaker(match.Groups[1].Value, gender) is { Name.Length: > 0 } shown ? shown.Name : match.Value);

        string? Maybe(string? text) => text is null ? null : Resolve(text);

        return this with
        {
            Secret = Resolve(Secret),
            End = Resolve(End),
            Beats = new StoryBeats
            {
                OpeningImage = Maybe(Beats.OpeningImage),
                ThemeStated = Maybe(Beats.ThemeStated),
                SetUp = Maybe(Beats.SetUp),
                Catalyst = Maybe(Beats.Catalyst),
                Debate = Maybe(Beats.Debate),
                BreakIntoTwo = Maybe(Beats.BreakIntoTwo),
                BStory = Maybe(Beats.BStory),
                FunAndGames = Maybe(Beats.FunAndGames),
                Midpoint = Maybe(Beats.Midpoint),
                BadGuysCloseIn = Maybe(Beats.BadGuysCloseIn),
                AllIsLost = Maybe(Beats.AllIsLost),
                DarkNightOfTheSoul = Maybe(Beats.DarkNightOfTheSoul),
                BreakIntoThree = Maybe(Beats.BreakIntoThree),
                Finale = Maybe(Beats.Finale),
                FinalImage = Maybe(Beats.FinalImage),
            },
            Scan = Scan is null ? null : Scan with { Text = Resolve(Scan.Text) },
            Clues = [.. Clues.Select(line => line with { Text = Resolve(line.Text) })],
            Finale = [.. Finale.Select(line => line with { Text = Resolve(line.Text) })],
            Options = [.. Options.Select(option => option with { Label = Resolve(option.Label), After = Resolve(option.After) })],
            Cast = [.. Cast.Select(speaker => speaker with { Who = Resolve(speaker.Who) })],
        };
    }

    /// <summary>A <c>{name:&lt;cast-id&gt;}</c> token; group 1 is the cast id.</summary>
    [GeneratedRegex(@"\{name:([^{}\s]+)\}")]
    public static partial Regex NameToken();

    /// <summary>The scan line, then every clue and finale line, named by its place.</summary>
    public IEnumerable<(string Field, StoryLine Line)> Lines() =>
        (Scan is null ? [] : new[] { ("scan", Scan) })
            .Concat(Clues.Select((line, at) => ($"clues[{at.ToString(CultureInfo.InvariantCulture)}]", line))
            .Concat(Finale.Select((line, at) => ($"finale[{at.ToString(CultureInfo.InvariantCulture)}]", line))));
}

/// <summary>
/// The stock stories downloaded into the stories folder: public cards from <c>index.json</c>, and each hidden layer
/// from <c>&lt;id&gt;.sealed</c> (raw deflate then base64).
/// </summary>
public sealed class StoryCatalog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Lazy<IReadOnlyDictionary<string, StorySecret>> _secrets;

    public StoryCatalog(IReadOnlyList<StoryCard> cards, Func<IReadOnlyList<StorySecret>> secrets)
    {
        Cards = cards;
        _secrets = new(() => secrets().ToDictionary(secret => secret.Id, StringComparer.OrdinalIgnoreCase));
    }

    public const string IndexFile = "index.json";

    public const string SealedExtension = ".sealed";

    /// <summary>
    /// The stories downloaded into <paramref name="folder"/>: cards from <c>index.json</c>, and the hidden entry of each
    /// <c>&lt;id&gt;.sealed</c> present. A card without its file is listed with no hidden entry; an entry that is
    /// unreadable or fails the format is not loaded and is logged by story id and field.
    /// </summary>
    public static StoryCatalog Load(string folder, ILogger? logger = null)
    {
        var cards = ReadIndex(folder, logger);
        var secrets = new List<StorySecret>();

        foreach (var card in cards)
        {
            var path = Path.Combine(folder, card.Id + SealedExtension);

            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var hidden = Unseal(File.ReadAllText(path, Encoding.ASCII)).FirstOrDefault(entry =>
                    string.Equals(entry.Id, card.Id, StringComparison.OrdinalIgnoreCase));

                if (hidden is null)
                {
                    logger?.LogWarning("Story {StoryId}: the hidden file has no entry for it.", card.Id);
                    continue;
                }

                var faults = Faults(card, hidden);

                if (faults.Count == 0)
                {
                    secrets.Add(hidden);
                    continue;
                }

                foreach (var fault in faults)
                {
                    logger?.LogWarning("Downloaded story not loaded: {Fault}", fault);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException or InvalidDataException)
            {
                logger?.LogWarning("Story {StoryId}: the hidden file could not be read ({Error}).", card.Id, ex.GetType().Name);
            }
        }

        return new StoryCatalog(cards, () => secrets);
    }

    private static IReadOnlyList<StoryCard> ReadIndex(string folder, ILogger? logger)
    {
        var path = Path.Combine(folder, IndexFile);

        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var stream = File.OpenRead(path);
            var cards = JsonSerializer.Deserialize<List<StoryCard>>(stream, Json) ?? [];
            return [.. cards.DistinctBy(card => card.Id, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger?.LogWarning("The downloaded story list could not be read ({Error}).", ex.GetType().Name);
            return [];
        }
    }

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
        var personas = Personas();

        foreach (var card in Cards)
        {
            faults.AddRange(CardRuleFaults(card));

            var pacing = StoryPacing.Find(card.Length);

            if (Secret(card.Id) is not { } hidden)
            {
                faults.Add($"{card.Id}: there is no hidden entry.");
            }
            else
            {
                faults.AddRange(CardFaults(card, hidden).Select(fault => $"{card.Id}: {fault}"));

                if (pacing is not null)
                {
                    faults.AddRange(PacingFaults(hidden, pacing).Select(fault => $"{card.Id}: {fault}"));
                }
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

    /// <summary>Every way one story breaks the format, named by field. Never quotes hidden text.</summary>
    public static IReadOnlyList<string> Faults(StoryCard card, StorySecret secret)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(secret);

        var faults = new List<string>(CardRuleFaults(card));
        faults.AddRange(CardFaults(card, secret).Select(fault => $"{card.Id}: {fault}"));

        if (StoryPacing.Find(card.Length) is { } pacing)
        {
            faults.AddRange(PacingFaults(secret, pacing).Select(fault => $"{card.Id}: {fault}"));
        }

        faults.AddRange(Faults(secret, Personas()).Select(fault => $"{card.Id}: {fault}"));
        return faults;
    }

    private static HashSet<string> Personas() =>
        new([PersonaCatalog.Covas.Id, .. PersonaCatalog.Shipped.Select(persona => persona.Id)], StringComparer.Ordinal);

    private static IEnumerable<string> CardRuleFaults(StoryCard card)
    {
        if (!StoryCard.Genres.Contains(card.Genre, StringComparer.Ordinal))
        {
            yield return $"{card.Id}: the genre is not one of the ten Save the Cat genres.";
        }

        if (!StoryCard.Levels.Contains(card.Level, StringComparer.Ordinal))
        {
            yield return $"{card.Id}: the level is missing or is not new, midrange or endgame.";
        }

        if (!PersonaCatalog.IsGuardian(card.Core) || card.Core == PersonaCatalog.Heretic.Id)
        {
            yield return $"{card.Id}: the core is missing, is not a Guardian core, or is the Heretic.";
        }

        if (string.IsNullOrWhiteSpace(card.Blurb))
        {
            yield return $"{card.Id}: the blurb is empty.";
        }

        if (StoryPacing.Find(card.Length) is null)
        {
            yield return $"{card.Id}: the length is missing or is not one of {string.Join(", ", StoryPacing.All.Select(length => length.Key))}.";
        }
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

            if (string.IsNullOrWhiteSpace(speaker.Id) || (speaker.Versions is null && string.IsNullOrWhiteSpace(speaker.Name)) || string.IsNullOrWhiteSpace(speaker.Who))
            {
                yield return $"{name} needs an id, a name and a who.";
            }
            else if (speakers.Contains(speaker.Id) || personas.Contains(speaker.Id))
            {
                yield return $"{name} has the id {speaker.Id}, which is taken by the ship, the narrator, a persona or another speaker.";
            }

            speakers.Add(speaker.Id ?? string.Empty);

            foreach (var fault in SoundFaults(name, speaker))
            {
                yield return fault;
            }

            if (speaker.Versions is not { } versions)
            {
                if (VoiceFault(name, speaker.Provider, speaker.Voice) is { } fault)
                {
                    yield return fault;
                }

                continue;
            }

            if (versions.ForMan is null || versions.ForWoman is null)
            {
                yield return $"{name} has one version, not forMan and forWoman.";
            }

            if (speaker.Name is not null || speaker.Voice is not null)
            {
                yield return $"{name} has versions and also a name or a voice of its own.";
            }

            foreach (var (key, version) in versions.All())
            {
                if (string.IsNullOrWhiteSpace(version.Name) || string.IsNullOrWhiteSpace(version.Voice))
                {
                    yield return $"{name}.{key} needs a name and a voice.";
                }
                else if (VoiceFault($"{name}.{key}", version.Provider ?? speaker.Provider, version.Voice) is { } fault)
                {
                    yield return fault;
                }
            }
        }

        var versioned = secret.Cast.Where(speaker => speaker.Versions is not null).Select(speaker => speaker.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var (field, text) in secret.Texts())
        {
            foreach (var cast in StorySecret.NameToken().Matches(text).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal))
            {
                if (!versioned.Contains(cast))
                {
                    yield return $"{field} names {cast} with a token, and {cast} has no versions.";
                }
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

    private static IEnumerable<string> SoundFaults(string name, StorySpeaker speaker)
    {
        if (speaker.Link is { } link && !(link >= 0 && link <= 1))
        {
            yield return $"{name} has a link outside 0 to 1.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var effect in speaker.Effects ?? [])
        {
            if (Audio.GuardianVoice.Find(effect.Id ?? string.Empty) is null)
            {
                yield return $"{name} has the effect {effect.Id}, which is not a Guardian effect.";
            }
            else if (!seen.Add(effect.Id!))
            {
                yield return $"{name} lists the effect {effect.Id} twice.";
            }

            if (effect.Level is < Audio.GuardianVoice.LowestLevel or > Audio.GuardianVoice.HighestLevel)
            {
                yield return $"{name} has the effect {effect.Id} at level {effect.Level.ToString(CultureInfo.InvariantCulture)}, outside {Audio.GuardianVoice.LowestLevel.ToString(CultureInfo.InvariantCulture)} to {Audio.GuardianVoice.HighestLevel.ToString(CultureInfo.InvariantCulture)}.";
            }
        }
    }

    /// <summary>Where a hidden entry does not fit its card's length: the scan line, the clue and finale counts, and exactly the length's beats.</summary>
    private static IEnumerable<string> PacingFaults(StorySecret secret, StoryPacing pacing)
    {
        var keys = pacing.BeatKeys;

        foreach (var (key, line) in secret.Beats.All)
        {
            var used = keys.Contains(key, StringComparer.Ordinal);

            if (used && string.IsNullOrWhiteSpace(line))
            {
                yield return $"beats.{key} is missing.";
            }
            else if (!used && !string.IsNullOrWhiteSpace(line))
            {
                yield return $"beats.{key} is not a beat of a {pacing.Name} story.";
            }
        }

        if (pacing.NarratedScan && string.IsNullOrWhiteSpace(secret.Scan?.Text))
        {
            yield return $"scan is missing; a {pacing.Name} story opens after a narrated beacon scan.";
        }
        else if (!pacing.NarratedScan && secret.Scan is not null)
        {
            yield return $"scan is set; a {pacing.Name} story flies to a real beacon.";
        }

        if (secret.Clues.Count != pacing.ClueDays.Count)
        {
            yield return $"clues has {secret.Clues.Count.ToString(CultureInfo.InvariantCulture)} lines, not {pacing.ClueDays.Count.ToString(CultureInfo.InvariantCulture)} for {pacing.Name}.";
        }

        if (secret.Finale.Count != pacing.FinaleChapters)
        {
            yield return $"finale has {secret.Finale.Count.ToString(CultureInfo.InvariantCulture)} lines, not {pacing.FinaleChapters.ToString(CultureInfo.InvariantCulture)} for {pacing.Name}.";
        }
    }

    private static string? VoiceFault(string name, string? provider, string? voice)
    {
        bool? voiceFits = provider switch
        {
            StorySpeaker.Kokoro => voice is not null && KokoroAssets.VoiceIds.Contains(voice, StringComparer.Ordinal),
            StorySpeaker.Chatterbox => !string.IsNullOrWhiteSpace(voice),
            _ => null,
        };

        return voiceFits switch
        {
            null => $"{name} has the provider {provider}, not {StorySpeaker.Kokoro} or {StorySpeaker.Chatterbox}.",
            false => $"{name} has a voice that {provider} does not have.",
            _ => null,
        };
    }

    /// <summary>Where a card names a member that has versions: by token, or by either version's name.</summary>
    private static IEnumerable<string> CardFaults(StoryCard card, StorySecret secret)
    {
        var expected = secret.Cast
            .Where(speaker => speaker.Primary)
            .SelectMany(speaker => speaker.Versions is { } versions
                ? versions.All().Select(version => $"{secret.Id}.{speaker.Id}.{version.Key}")
                : [$"{secret.Id}.{speaker.Id}"])
            .ToHashSet(StringComparer.Ordinal);

        foreach (var picture in card.CastPictures.Where(picture => !expected.Contains(picture)))
        {
            yield return $"castPictures names {picture}, which is not a primary cast member or a version of one.";
        }

        foreach (var picture in expected.Where(picture => !card.CastPictures.Contains(picture, StringComparer.Ordinal)))
        {
            yield return $"castPictures is missing {picture}, a primary cast member's picture.";
        }

        foreach (var speaker in secret.Cast.Where(speaker => speaker.Versions is not null))
        {
            var names = speaker.Versions!.All().Select(version => version.Version.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();

            foreach (var (field, text) in new[] { ("blurb", card.Blurb), ("inYourWords", card.InYourWords), ("beacon", card.Beacon) })
            {
                if (text is null)
                {
                    continue;
                }

                if (StorySecret.NameToken().IsMatch(text)
                    || names.Any(name => Regex.IsMatch(text, $@"\b{Regex.Escape(name)}\b", RegexOptions.CultureInvariant)))
                {
                    yield return $"{field} names {speaker.Id}, who has versions; call them by role.";
                }
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
}
