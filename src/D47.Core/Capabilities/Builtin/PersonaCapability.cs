using System.Globalization;

using System.Text;
using D47.Core.Configuration;
using D47.Core.Listening;
using D47.Core.Persona;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Which companion character is aboard (Phase 11, "Personas").</summary>
public static class PersonaCapability
{
    public const string Id = "persona";

    public const string PersonaKey = "persona.id";

    public const string IntroductionsKey = "persona.introductions";

    public const string CoreHumorKey = "persona.coreHumor";

    public const string CoreHumorPercentKey = "persona.coreHumorPercent";

    public const string NpcHumorKey = "persona.npcHumor";

    public const string NpcHumorPercentKey = "persona.npcHumorPercent";

    public const string CarrierHumorKey = "persona.carrierHumor";

    public const string CarrierHumorPercentKey = "persona.carrierHumorPercent";

    /// <summary>The row that reads what the ship the Commander is in flies with, and binds it.</summary>
    public const string ShipCoreKey = "persona.shipCore";

    /// <summary>The row that reads every binding, and forgets the current ship's.</summary>
    public const string ShipCoresKey = "persona.shipCores";

    /// <summary>The row that picks which ship the core row above is about.</summary>
    public const string ShipCoreShipKey = "persona.shipCoreShip";

    /// <summary>The row that reads the cores the Commander wrote, and opens the editor.</summary>
    public const string OwnKey = "persona.own";

    public const string AvatarClipsKey = "persona.avatarClips";

    /// <summary>The core-row value that means "take the binding back".</summary>
    private const string Nobody = "nobody";

    /// <summary>A ship id as a choice key.</summary>
    private static string Keyed(int shipId) => shipId.ToString(CultureInfo.InvariantCulture);

    public static CapabilityDescriptor Create(
        PersonaHost host,
        SettingsService settings,
        ShipCoreService? ships = null) => new()
    {
        Id = Id,
        Group = "Conversation",
        Name = "Core",
        Summary = "Report which core is aboard, what it is called, and how to change it.",
        Examples = ["who are you", "which core is this", "switch to Cora"],
        // Each names its tool (#161). state_identity is what all five already reached, and saying so is what
        // stops the next tool declared above it silently taking them — bind_ship_core and forget_ship_core
        // both take no required argument and both write.
        Keywords =
        [
            new("who are you", "state_identity"),
            new("which persona", "state_identity"),
            new("what persona", "state_identity"),
            new("which core", "state_identity"),
            new("who am I talking to", "state_identity"),
        ],
        Display = new CapabilityDisplay { PanelTitle = "Core", Order = 12 },
        Tools =
        [
            // First, and the order matters rather than being cosmetic: the keyword router answers with a
            // capability's first tool that needs no arguments, and every phrase this capability declares —
            // "who are you", "which core", "who am I talking to" — is a question about identity with a
            // one-sentence answer.
            new ToolDefinition
            {
                Name = "state_identity",
                Description =
                    "Answer who you are. The name of this core, in one "
                    + "sentence, and nothing else — no status, no list of what else is available.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok($"I am {host.Current.Name}.")),
            },
            new ToolDefinition
            {
                Name = "describe_persona",
                Description =
                    "Report which core is currently active and which "
                    + "other cores are available to switch to.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(host, settings.Current, ships))),
            },
        ],
        Settings = Rows(host, ships),
    };

    private static IReadOnlyList<SettingRow> Rows(PersonaHost host, ShipCoreService? ships) =>
    [
        new SettingRow
        {
            Key = PersonaKey,
            Label = "Core",
            Help =
                "Which core answers you. Each keeps its own memory of your conversations. While a story "
                + "runs, COVAS answers and the Guardian cores are locked until you scan a Guardian beacon.",
            Kind = SettingKind.Choice,
            DefaultDisplay = PersonaCatalog.Resolve(null).Name,
            DocsAnchor = "persona",
            // A source rather than a list, so a core the Commander wrote appears in the picker the moment
            // they write it (remediation.md 11, item 9).
            Choices = [.. PersonaCatalog.Shipped.Select(persona => persona.Id)],
            ChoiceSource = _ => [.. PersonaCatalog.All.Select(persona => persona.Id)],
            ChoiceLabel = id => PersonaCatalog.Resolve(id).Name,
            ChoiceStatus = (_, id) => Locked(host, id),
            Refuses = (_, id) => WhyAsleep(host, id),

            // Protected, and this is the one row in the phase where that is a judgement call rather than a
            // rule being followed.
            Protected = true,
            // The stock core and the shipped cores. A core the Commander wrote is reached by sound, below.
            Commands = [.. PersonaCatalog.Stocked.SelectMany(SelectionPhrases)],
            ValuesBySound = CoresHeardIn,
            Binding = new SettingBinding
            {
                Read = s => host.Cores.Admit(PersonaCatalog.Resolve(s.Persona.Id)).Id,
                Write = WriteCoreAboard,
            },
        },
        ..HumorRows(),
        new SettingRow
        {
            Key = IntroductionsKey,
            Advanced = true,
            Label = "Introductions",
            Help =
                "A core introduces itself the first time you ever pick it, and reacts to the gap "
                + "every time after that. That is remembered between sessions, so restarting d47 "
                + "no longer brings the opening lines back — forgetting is the only way to hear "
                + "them again, and it puts every core back to its introduction at once.",
            Kind = SettingKind.Info,
            DocsAnchor = "introductions",
            PressLabel = "Forget introductions",
            Press = host.ForgetIntroductions,

            // Info, so the model cannot reach it — same reason the persona row above is protected, and here
            // it comes free rather than as a flag.
            Binding = new SettingBinding { Read = _ => Introductions(host).Block },
            Note = _ => Introductions(host).Second,
            Destructive = true,
            PressEnabled = _ => host.Introduced.Count > 0,
        },
        new SettingRow
        {
            Key = OwnKey,
            Advanced = true,
            Label = "Custom cores",
            LabelFor = _ => $"Custom cores ({OwnNames().Count})",
            Help =
                "Cores you wrote, which join the picker at the top of this section beside the "
                + "cores that ship. One needs a name and a paragraph saying what it is like; "
                + "everything else about the frame is supplied, and the shared preamble and "
                + "standing instructions wrap what you write exactly as they wrap a shipped core. "
                + "The file behind it is personas.json beside d47.exe, and the editor is a "
                + "convenience over that file rather than an alternative to it.",
            Kind = SettingKind.Info,
            DocsAnchor = "custom-cores",

            // Info, so the model cannot reach it.
            Binding = new SettingBinding { Read = _ => SummariseOwn() },
        },
        new SettingRow
        {
            Key = AvatarClipsKey,
            Label = "Avatar animations",
            Help =
                "The face on the panel plays a short clip for the core aboard in each state. D47 fetches "
                + "the clips of the core aboard that it does not have yet, from the same GitHub release the "
                + "app updates itself from, and keeps them in data\\avatar-clips.\n\n"
                + "Off, nothing is downloaded and the avatar draws its own mark for any state with no clip. "
                + "Clips already on disk still play.",
            Kind = SettingKind.Toggle,
            DocsAnchor = "avatar-animations",
            EgressId = EgressDisclosure.AvatarClips,
            Binding = new SettingBinding
            {
                Read = s => s.Ui.AvatarClips ? "true" : "false",
                Write = (s, v) => s with
                {
                    Ui = s.Ui with { AvatarClips = bool.TryParse(v, out var on) && on },
                },
            },
        },
        ..ships is null ? Array.Empty<SettingRow>() : ShipCoreRows(host, ships),
    ];

    /// <summary>A level and a frequency for each group. Not protected: either can be changed back on the panel.</summary>
    private static SettingRow[] HumorRows() =>
    [
        HumorLevel(
            CoreHumorKey,
            "Core humor",
            "How funny the core may be, from 0 (serious) to 10 (comedian). COVAS ignores it.",
            s => s.CoreHumor,
            (p, v) => p with { CoreHumor = v },
            [new SettingCommandPhrase("humor on", "3"), new SettingCommandPhrase("humor off", "0")]),
        HumorPercent(
            CoreHumorPercentKey,
            "Core humor frequency",
            "The share of the core's lines that get humor. d47 decides line by line.",
            s => s.CoreHumor,
            s => s.CoreHumorPercent,
            (p, v) => p with { CoreHumorPercent = v }),
        HumorLevel(
            NpcHumorKey,
            "NPC humor",
            "How funny NPCs may be - chatter, hails, and the canned lines d47 rewords - from 0 "
            + "(serious) to 10 (comedian).",
            s => s.NpcHumor,
            (p, v) => p with { NpcHumor = v },
            []),
        HumorPercent(
            NpcHumorPercentKey,
            "NPC humor frequency",
            "The share of NPC lines that get humor.",
            s => s.NpcHumor,
            s => s.NpcHumorPercent,
            (p, v) => p with { NpcHumorPercent = v }),
        HumorLevel(
            CarrierHumorKey,
            "Carrier crew humor",
            "How funny your carrier's captain and tower may be, from 0 (serious) to 10 (comedian).",
            s => s.CarrierHumor,
            (p, v) => p with { CarrierHumor = v },
            []),
        HumorPercent(
            CarrierHumorPercentKey,
            "Carrier crew humor frequency",
            "The share of the captain's and tower's lines that get humor.",
            s => s.CarrierHumor,
            s => s.CarrierHumorPercent,
            (p, v) => p with { CarrierHumorPercent = v }),
    ];

    private static SettingRow HumorLevel(
        string key,
        string label,
        string help,
        Func<PersonaSettings, int> read,
        Func<PersonaSettings, int, PersonaSettings> write,
        IReadOnlyList<SettingCommandPhrase> commands) => new()
    {
        Key = key,
        Advanced = true,
        Label = label,
        Help = help + " Warnings never get humor.",
        Kind = SettingKind.Number,
        DefaultDisplay = "0",
        DocsAnchor = "humor",
        Group = "Humor",
        Minimum = 0,
        Maximum = Humor.Top,
        ScaleEnds = ("Serious", "Comedian"),
        Commands = commands,
        Binding = new SettingBinding
        {
            Read = s => read(s.Persona).ToString(CultureInfo.InvariantCulture),
            Write = (s, v) => s with
            {
                Persona = write(s.Persona, Clamped(v, Humor.Top, read(s.Persona))),
            },
        },
    };

    private static SettingRow HumorPercent(
        string key,
        string label,
        string help,
        Func<PersonaSettings, int> level,
        Func<PersonaSettings, int> read,
        Func<PersonaSettings, int, PersonaSettings> write) => new()
    {
        Key = key,
        Advanced = true,
        Label = label,
        Help = help,
        Kind = SettingKind.Number,
        DefaultDisplay = PersonaSettings.DefaultHumorPercent.ToString(CultureInfo.InvariantCulture),
        DocsAnchor = "humor",
        Group = "Humor",
        Minimum = 0,
        Maximum = 100,
        Unit = "%",
        DisabledWhen = s => level(s.Persona) == 0,
        Binding = new SettingBinding
        {
            Read = s => read(s.Persona).ToString(CultureInfo.InvariantCulture),
            Write = (s, v) => s with
            {
                Persona = write(s.Persona, Clamped(v, 100, read(s.Persona))),
            },
        },
    };

    /// <summary>A whole number from 0 to <paramref name="most"/>, or <paramref name="unchanged"/> when it will not parse.</summary>
    private static int Clamped(string? value, int most, int unchanged) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 0, most)
            : unchanged;

    /// <summary>A core per ship, on the panel (Phase 35).</summary>
    private static SettingRow[] ShipCoreRows(PersonaHost host, ShipCoreService ships) =>
    [
        // Two rows, and they read as one sentence: which ship, and who flies it.
        new SettingRow
        {
            Key = ShipCoreShipKey,
            Advanced = true,
            Label = "Ship",
            Help =
                "Which ship the core below belongs to. Every ship in your fleet is here, so you do "
                + "not have to be sitting in one to give it a core.",
            Kind = SettingKind.Choice,
            DocsAnchor = "core-for-this-ship",

            // What puts a rule between this pair and the persona rows above (remediation.md 16, item 3).
            Group = "A core per ship",

            // Protected, which is what keeps Phase 35's rule after these stopped being Info rows.
            Protected = true,

            // A ship id, which only means something for the Commander whose fleet it counts (Phase 44): one
            // Commander's ship 7 and another's are two ships.
            Scope = SettingScope.Commander,
            ChoiceSource = _ => [.. ships.Fleet().Select(entry => Keyed(entry.ShipId))],
            ChoiceLabel = key => ships.Fleet()
                .FirstOrDefault(entry => Keyed(entry.ShipId) == key)
                .Said ?? key,
            ChoiceStatus = (_, key) => ShipStatus(ships, key),
            WhyNoChoices = _ =>
                "I have not seen your fleet yet. Dock somewhere with a shipyard and I will read it.",
            Binding = new SettingBinding
            {
                Read = settings => ShipShown(settings, ships) is var id and not 0 ? Keyed(id) : null,
                Write = (settings, value) => settings with
                {
                    Persona = settings.Persona with
                    {
                        ShipCoreShip = int.TryParse(value, CultureInfo.InvariantCulture, out var id) ? id : 0,
                    },
                },
            },
        },
        new SettingRow
        {
            Key = ShipCoreKey,
            Advanced = true,
            Label = "Core for that ship",
            LabelFor = settings => ShipShown(settings, ships) is var id and not 0
                && ships.Fleet().FirstOrDefault(entry => entry.ShipId == id).Said is { Length: > 0 } said
                    ? $"Core for {said}"
                    : "Core for that ship",
            Help =
                "A ship can have its own core, so changing ship changes who answers "
                + "you. Nothing is bound until you say so, and boarding a ship you have not bound "
                + "leaves whoever is aboard aboard. Choosing nobody takes a binding back.",
            Kind = SettingKind.Choice,
            DocsAnchor = "core-for-this-ship",
            Protected = true,

            // Per Commander through the store rather than the settings file: what it reads is the binding for
            // the ship the row above points at, and ship-cores.json carries the Commander beside each one
            // (Phase 44).
            Scope = SettingScope.Commander,

            // The same group as the row above, so the pair sits under one heading and the rule is drawn once
            // rather than between two rows that are one thought.
            Group = "A core per ship",

            // Nobody first: it is the unbind.
            Choices = [Nobody, .. PersonaCatalog.Shipped.Select(persona => persona.Id)],
            ChoiceSource = _ => [Nobody, .. PersonaCatalog.All.Select(persona => persona.Id)],
            ChoiceLabel = id => id == Nobody
                ? "Nobody — whoever is aboard stays aboard"
                : PersonaCatalog.Resolve(id).Name,
            ChoiceStatus = (_, id) => id == Nobody ? null : Locked(host, id),
            Refuses = (_, id) => id == Nobody ? null : WhyAsleep(host, id),
            Binding = new SettingBinding
            {
                Read = settings => ShipShown(settings, ships) is var id and not 0
                    ? ships.For(id)?.Core ?? Nobody
                    : null,

                // The write is the binding, and it returns the settings unchanged: what it changes lives in
                // ship-cores.json, not here.
                Write = (settings, value) =>
                {
                    if (ShipShown(settings, ships) is var id and not 0)
                    {
                        ships.BindTo(id, value == Nobody ? null : value);
                    }

                    return settings;
                },
            },
        },
        new SettingRow
        {
            Key = ShipCoresKey,
            Advanced = true,
            Label = "Cores by ship",
            Help =
                "Every ship you have bound, and what it flies with. The file behind it is "
                + "ship-cores.json beside d47.exe, and it is meant to be readable — one line per "
                + "ship, hand-editable, with the hull and the name written beside the id so you "
                + "can tell which ship is which.",
            Kind = SettingKind.Info,
            DocsAnchor = "cores-by-ship",
            Binding = new SettingBinding { Read = _ => ships.Status().Block },
            Note = _ => ships.Status().Second,
            Problems = () => [.. ships.Store.Problems.Select(problem => (problem.Where, problem.Reason))],
        },
    ];

    /// <summary>The ship the Ship row shows: the Commander's pick, else the ship aboard, else 0.</summary>
    private static int ShipShown(D47Settings settings, ShipCoreService ships) =>
        settings.Persona.ShipCoreShip is not 0 and var picked ? picked : ships.CurrentShipId ?? 0;

    /// <summary>A ship's bound core, or NO CORE; the ship aboard reads "· ABOARD" in cyan.</summary>
    private static ChoiceStatus? ShipStatus(ShipCoreService ships, string key)
    {
        if (!int.TryParse(key, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        var core = ships.For(id) is { } binding ? PersonaCatalog.Resolve(binding.Core).Name : null;

        return ships.CurrentShipId == id
            ? new ChoiceStatus(core is null ? "NO CORE · aboard" : $"{core} · aboard", ChoiceTone.Cyan)
            : core is null
                ? new ChoiceStatus("NO CORE", ChoiceTone.Grey)
                : new ChoiceStatus(core, ChoiceTone.Accent);
    }

    /// <summary>The status drawn under a core the running story holds back.</summary>
    private static ChoiceStatus? Locked(PersonaHost host, string id) =>
        host.Cores.IsAwake(PersonaCatalog.Resolve(id)) ? null : new ChoiceStatus("LOCKED", ChoiceTone.Grey);

    /// <summary>Why a core cannot be chosen now, or null where it can.</summary>
    private static string? WhyAsleep(PersonaHost host, string id)
    {
        var persona = PersonaCatalog.Resolve(id);
        var hold = host.Cores.Hold;

        if (!GuardianCores.IsHeld(persona, hold))
        {
            return null;
        }

        var until = hold.Cores == HeldCores.Heretic && !hold.Narrated
            ? "until you scan a Guardian beacon in a second system"
            : "until you scan a Guardian beacon";

        return $"{persona.Name} is held back while {hold.Story} runs, {until}. Pause or abandon the story to have it back now.";
    }

    /// <summary>What the introductions row states.</summary>
    private static StatusText Introductions(PersonaHost host) => StatusRows.Introductions(host.Introduced.Count);

    /// <summary>The names of the cores the Commander wrote, in the editor's order.</summary>
    public static IReadOnlyList<string> OwnNames() =>
        Persona.PersonaCatalog.Own?.Invoke().Select(core => core.Name).ToArray() ?? [];

    /// <summary>What the own-cores row reads.</summary>
    public static string SummariseOwn() => StatusRows.OwnCores(OwnNames());

    /// <summary>The closed phrase set that reaches this row without a model.</summary>
    private static IEnumerable<SettingCommandPhrase> SelectionPhrases(Persona.Persona persona)
    {
        var name = persona.Name.ToLowerInvariant();

        yield return new SettingCommandPhrase($"switch to {name}", persona.Id);
        yield return new SettingCommandPhrase($"be {name}", persona.Id);
        yield return new SettingCommandPhrase($"become {name}", persona.Id);
        yield return new SettingCommandPhrase($"persona {name}", persona.Id);
        yield return new SettingCommandPhrase($"wake {name}", persona.Id);

        // "The Heretic" and "L-LAM-0" both read badly after "switch to the", and a Commander saying the bare
        // name is the likeliest phrasing for every core.
        if (name.StartsWith("the ", StringComparison.Ordinal))
        {
            var bare = name[4..];

            yield return new SettingCommandPhrase($"switch to {bare}", persona.Id);
            yield return new SettingCommandPhrase($"be {bare}", persona.Id);
            yield return new SettingCommandPhrase($"become {bare}", persona.Id);
        }
    }

    /// <summary>
    /// The cores "switch to X" names when X is compared by sound with each core's name, the closest ones only:
    /// "switch to Quora" is Cora. Certain when one core sounds exactly like X and X is spelt within two letters
    /// of its name, one for a name of three letters.
    /// </summary>
    internal static ValuesHeard CoresHeardIn(string said)
    {
        const string Opener = "switch to ";

        var lowered = said.ToLowerInvariant();

        if (!lowered.StartsWith(Opener, StringComparison.Ordinal))
        {
            return ValuesHeard.None;
        }

        var rest = lowered[Opener.Length..];

        if (rest.StartsWith("the ", StringComparison.Ordinal))
        {
            rest = rest[4..];
        }

        var heard = PhoneticKey.Of(rest);

        if (heard.Length == 0)
        {
            return ValuesHeard.None;
        }

        // Starting on the same sound, so "switch to commander" is not Mender.
        var near = PersonaCatalog.All
            .Select(core => (Core: core, Distance: SpokenNames(core)
                .Select(PhoneticKey.Of)
                .Where(key => key.Length > 0 && key[0] == heard[0])
                .Select(key => (Apart: PhoneticKey.Distance(heard, key), Allowed: PhoneticKey.Tolerance(key)))
                .Where(score => score.Apart <= score.Allowed)
                .Select(score => (int?)score.Apart)
                .Min()))
            .Where(scored => scored.Distance is not null)
            .ToList();

        if (near.Count == 0)
        {
            return ValuesHeard.None;
        }

        var closest = near.Min(scored => scored.Distance);
        var cores = near.Where(scored => scored.Distance == closest).Select(scored => scored.Core).ToList();

        var spelt = Letters(rest);
        var certain = closest == 0 && cores is [var only] && SpokenNames(only).Select(Letters).Any(name =>
            PhoneticKey.Distance(spelt, name) <= (name.Length <= 3 ? 1 : 2));

        return new ValuesHeard([.. cores.Select(core => core.Id)], certain);
    }

    /// <summary>What a Commander calls a core: its name, without "The" if it has one, and a shipped core's id.</summary>
    private static IEnumerable<string> SpokenNames(Persona.Persona core)
    {
        yield return core.Name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? core.Name[4..] : core.Name;

        if (PersonaCatalog.Stocked.Any(stocked => string.Equals(stocked.Id, core.Id, StringComparison.Ordinal)))
        {
            yield return core.Id;
        }
    }

    private static string Letters(string text) => new([.. text.ToLowerInvariant().Where(char.IsAsciiLetterLower)]);

    private static D47Settings WriteCoreAboard(D47Settings settings, string? value) =>
        settings with
        {
            Persona = settings.Persona with { Id = PersonaCatalog.Knows(value) ? value! : PersonaCatalog.DefaultId },
        };

    private static string Describe(PersonaHost host, D47Settings settings, ShipCoreService? ships)
    {
        var report = new StringBuilder();
        var current = host.Current;

        report.AppendLine($"Active core: {current.Name} — {current.Tagline}");

        report.AppendLine(settings.Llm.PersonalityEnabled
            ? "Personality is on."
            : "Personality is off, so you are answering plainly and this core's voice is not in play.");

        // The binding, read and never written (Phase 35).
        if (ships is not null)
        {
            report.AppendLine(ships.DescribeCurrent()
                + " That is the Commander's own binding — you can say what it is, and only they can change it.");
        }

        report.AppendLine();
        report.AppendLine("Available personas (the Commander changes these from the panel or by saying so):");

        foreach (var persona in PersonaCatalog.All.Where(host.Cores.IsAwake))
        {
            report.AppendLine($"  {persona.Id} — {persona.Name}: {persona.Tagline}");
        }

        return report.ToString().TrimEnd();
    }
}
