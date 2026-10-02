using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Persona;
using D47.Core.Speech;

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

    /// <summary>
    /// The member as a Commander of <paramref name="gender"/> meets them. An unset gender meets the
    /// <see cref="StorySpeakerVersions.ForMan"/> version.
    /// </summary>
    public StorySpeakerShown Shown(string storyId, string? gender)
    {
        var woman = gender == CommanderGender.Woman;

        if (Versions is not { } versions || (woman ? versions.ForWoman ?? versions.ForMan : versions.ForMan ?? versions.ForWoman) is not { } version)
        {
            return new StorySpeakerShown(Id, Name ?? string.Empty, Provider, Voice ?? string.Empty, $"{storyId}.{Id}");
        }

        var key = woman && versions.ForWoman is not null ? StorySpeakerVersions.ForWomanKey : StorySpeakerVersions.ForManKey;
        return new StorySpeakerShown(Id, version.Name, version.Provider ?? Provider, version.Voice, $"{storyId}.{Id}.{key}");
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
/// A cast member as the current Commander meets them. <see cref="Picture"/> names
/// <c>assets/stories/&lt;Picture&gt;.png</c>.
/// </summary>
public sealed record StorySpeakerShown(string Id, string Name, string Provider, string Voice, string Picture);

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

            if (!StoryCard.Levels.Contains(card.Level, StringComparer.Ordinal))
            {
                faults.Add($"{card.Id}: the level is missing or is not new, midrange or endgame.");
            }

            if (!PersonaCatalog.IsGuardian(card.Core) || card.Core == PersonaCatalog.Heretic.Id)
            {
                faults.Add($"{card.Id}: the core is missing, is not a Guardian core, or is the Heretic.");
            }

            if (string.IsNullOrWhiteSpace(card.Blurb))
            {
                faults.Add($"{card.Id}: the blurb is empty.");
            }

            var pacing = StoryPacing.Find(card.Length);

            if (pacing is null)
            {
                faults.Add($"{card.Id}: the length is missing or is not one of {string.Join(", ", StoryPacing.All.Select(length => length.Key))}.");
            }

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
