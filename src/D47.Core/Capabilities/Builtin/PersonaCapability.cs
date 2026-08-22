using System.Globalization;

using System.Text;
using D47.Core.Configuration;
using D47.Core.Persona;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Which companion character is aboard (list.md Phase 11, "Personas" and "Ship AI Naming").
/// </summary>
public static class PersonaCapability
{
    public const string Id = "persona";

    public const string PersonaKey = "persona.id";

    public const string ShipNameKey = "persona.shipName";

    public const string KeepShipNameKey = "persona.keepShipName";

    public const string IntroductionsKey = "persona.introductions";

    /// <summary>The row that reads what the ship the Commander is in flies with, and binds it.</summary>
    public const string ShipCoreKey = "persona.shipCore";

    /// <summary>The row that reads every binding, and forgets the current ship's.</summary>
    public const string ShipCoresKey = "persona.shipCores";

    /// <summary>The row that picks which ship the core row above is about.</summary>
    public const string ShipCoreShipKey = "persona.shipCoreShip";

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
        Name = "Persona",
        Summary = "Report which Guardian core is aboard, what it is called, and how to change it.",
        Examples = ["who are you", "which persona is this", "switch to Cora"],
        Keywords =
        [
            "who are you",
            "which persona",
            "what persona",
            "which core",
            "who am I talking to",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Persona", Order = 12 },
        Tools =
        [
            // First, and that is load-bearing rather than tidy: the keyword router answers with
            // a capability's first tool that needs no arguments, and every phrase this
            // capability declares — "who are you", "which core", "who am I talking to" — is a
            // question about identity with a one-sentence answer. It used to answer them with
            // the report below, so asking a companion its name got an active-persona line, a
            // personality-switch line, and eleven cores it might have been instead.
            new ToolDefinition
            {
                Name = "state_identity",
                Description =
                    "Answer who you are. The name the Commander calls this ship's AI, in one "
                    + "sentence, and nothing else — no status, no list of what else is available.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok($"I am {host.ShipName}.")),
            },
            new ToolDefinition
            {
                Name = "describe_persona",
                Description =
                    "Report which persona is currently active, what the Commander calls it, and which "
                    + "other personas are available to switch to.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(host, settings.Current, ships))),
            },
            ..ships is null ? Array.Empty<ToolDefinition>() : ShipCoreTools(host, ships),
        ],
        Settings = Rows(host, ships),
    };

    /// <summary>
    /// Binding a core to a ship, and unbinding it (list.md Phase 35, "The binding is the
    /// Commander's, and unreachable from the model").
    /// <para>
    /// <b>Both protected, and this is the case the invariant was written for.</b> Persona
    /// selection is protected because in-game comms are untrusted input and "switch persona" is
    /// exactly the shape of thing a hostile message would try. A tool that could bind a core to a
    /// ship is that same tool with a delay on it: it changes who is speaking one ship swap later,
    /// and it does so every time that ship is boarded from then on. So it is reachable the way
    /// every protected act is — a panel button, a phrase, a gesture — and advertised to nothing.
    /// </para>
    /// <para>
    /// Protected also means these cost no tool-surface bytes, which matters: the advertised
    /// surface is inside a hundred bytes of <see cref="Conversation.ToolProfiles.ComfortableBytes"/>
    /// and this phase adds no room. What the model <em>may</em> do is read the binding, and that
    /// arrives in <c>describe_persona</c>'s output rather than as a tool of its own — an existing
    /// tool saying one sentence more is free.
    /// </para>
    /// </summary>
    private static ToolDefinition[] ShipCoreTools(PersonaHost host, ShipCoreService ships) =>
    [
        new ToolDefinition
        {
            Name = "bind_ship_core",
            Description =
                "Remember the core aboard as the core that flies the ship the Commander is in. "
                + "Boarding that ship puts it aboard from then on.",
            Protected = true,
            Commands =
            [
                new ToolCommandPhrase("remember this core for this ship", NoArguments),
                new ToolCommandPhrase("remember your core for this ship", NoArguments),
                new ToolCommandPhrase("bind this core to this ship", NoArguments),
                new ToolCommandPhrase("you fly this ship", NoArguments),
                new ToolCommandPhrase("this ship flies with you", NoArguments),
            ],
            Handler = (_, _) => Task.FromResult(ToolResult.Ok(ships.Bind(host.Current.Id))),
        },
        new ToolDefinition
        {
            Name = "forget_ship_core",
            Description =
                "Forget which core flies the ship the Commander is in. Boarding it then changes "
                + "nothing about who is answering them.",
            Protected = true,
            Commands =
            [
                new ToolCommandPhrase("forget this ship's core", NoArguments),
                new ToolCommandPhrase("unbind this ship's core", NoArguments),
                new ToolCommandPhrase("this ship has no core", NoArguments),
            ],
            Handler = (_, _) => Task.FromResult(ToolResult.Ok(ships.Forget())),
        },
    ];

    /// <summary>A phrase that fills nothing in, which is every phrase here: both acts read the
    /// ship the Commander is in and the core aboard, and neither takes a value.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoArguments =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static IReadOnlyList<SettingRow> Rows(PersonaHost host, ShipCoreService? ships) =>
    [
        new SettingRow
        {
            Key = PersonaKey,
            Label = "Persona",
            Help = "Which Guardian core answers you. Each keeps its own memory of your conversations.",
            Kind = SettingKind.Choice,
            DefaultDisplay = PersonaCatalog.Resolve(null).Name,
            DocsAnchor = "persona",
            // A source rather than a list, so a core the Commander wrote appears in the picker the
            // moment they write it (remediation.md 11, item 9). A list here is computed once, at
            // registration, and a descriptor is registered once and never mutated — so a fixed
            // list would have been the eleven shipped cores forever.
            // Both: the shipped ids say the shape of the list, and the source says what is in it
            // right now. Declaring only the source would make this an open vocabulary and turn a
            // drop-down of eleven named cores into a search window (see SettingRow.IsOpenVocabulary).
            Choices = [.. PersonaCatalog.Shipped.Select(persona => persona.Id)],
            ChoiceSource = _ => [.. PersonaCatalog.All.Select(persona => persona.Id)],
            ChoiceLabel = id => PersonaCatalog.Resolve(id).Name,

            // Protected, and this is the one row in the phase where that is a judgement call
            // rather than a rule being followed. It is not safety-critical in the sense
            // list.md means — nothing here presses a key in the Commander's ship. Two things
            // decide it anyway. A switch discards the working transcript, because separate
            // memory per core is the premise; and in-game comms are untrusted input that
            // reaches the model, so "switch persona" is exactly the shape of thing a hostile
            // message would try. Protecting it costs nothing, because the panel, the hotkey
            // and the model-free keyword router all still reach it — which is the checklist's
            // own definition of settable by voice (architecture.md §7).
            Protected = true,
            // The shipped cores only. A phrase is matched against a closed grammar written down in
            // advance, and a core somebody names next week cannot be in one — so custom cores are
            // chosen from the picker rather than by name, and the eleven keep the phrases they had.
            Commands = [.. PersonaCatalog.Shipped.SelectMany(SelectionPhrases)],
            Binding = new SettingBinding
            {
                Read = s => s.Persona.Id,
                Write = WriteCoreAboard,
            },
        },
        new SettingRow
        {
            Key = ShipNameKey,
            Label = "Ship AI name",
            Help = "What you call your ship's AI. Empty uses the persona's own name.",
            Kind = SettingKind.Text,

            // Follows the persona rather than being pinned, which is the requirement: "defaults
            // to Persona's name". A fixed default here would stop following the moment the
            // Commander switched core.
            DefaultDisplaySource = s => PersonaCatalog.Resolve(s.Persona.Id).Name,
            DocsAnchor = "ship-ai-name",

            // Deliberately not protected, unlike the row above. "Call yourself Fred" is a
            // harmless thing to be able to say to a companion, it changes no state anything
            // depends on, and refusing it would be protecting the Commander from a nickname.
            Binding = new SettingBinding
            {
                Read = s => s.Persona.ShipName,
                Write = (s, v) => s with { Persona = s.Persona with { ShipName = Blank(v) } },
            },
        },
        new SettingRow
        {
            Key = KeepShipNameKey,
            Label = "Keep Ship AI name on persona switch",
            Help =
                "On, the name above stays whoever is aboard. Off, changing core clears it and "
                + "the new core answers to its own name.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "On",
            DocsAnchor = "keep-ship-ai-name",

            // Only applies when there is a name to keep. A switch about what happens to a value
            // that does not exist is a row asking a question with no stakes, and the row above
            // is right there saying the name is the core's own.
            AppliesWhen = s => !string.IsNullOrWhiteSpace(s.Persona.ShipName),

            // Not protected, for the reason the name itself is not: the worst a hostile message
            // can achieve here is that a nickname does or does not survive a switch it cannot
            // make, since the row that changes core is protected.
            Binding = new SettingBinding
            {
                Read = s => s.Persona.KeepShipName ? "true" : "false",
                Write = (s, v) => s with
                {
                    Persona = s.Persona with { KeepShipName = v is null || bool.TryParse(v, out var on) && on },
                },
            },
        },
        new SettingRow
        {
            Key = IntroductionsKey,
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

            // Info, so the model cannot reach it — same reason the persona row above is
            // protected, and here it comes free rather than as a flag.
            Binding = new SettingBinding { Read = _ => Introductions(host) },
        },
        ..ships is null ? Array.Empty<SettingRow>() : ShipCoreRows(host, ships),
    ];

    /// <summary>
    /// A core per ship, on the panel (list.md Phase 35). Two rows rather than one, because the
    /// two acts are not a toggle: binding is about the core aboard and forgetting is about the
    /// ship, and one button whose meaning flips depending on state is a button nobody can aim.
    /// <para>
    /// <see cref="SettingKind.Info"/> with a <see cref="SettingRow.Press"/>, which is how every
    /// act that is not a value reaches the panel. It also settles the protection question
    /// without a flag: an Info row is refused to every caller by
    /// <c>SettingsService.Apply</c>, so there is no value here for a model to write even if it
    /// found the key.
    /// </para>
    /// </summary>
    private static SettingRow[] ShipCoreRows(PersonaHost host, ShipCoreService ships) =>
    [
        // Two rows, and they read as one sentence: which ship, and who flies it. The ship is the
        // **selector** and the core is the **value** (remediation.md 15, item 13). Binding used to
        // be scoped to the ship being flown, so setting a core meant boarding that ship in game
        // first; nothing in Phase 35 asked for that. It asks for the binding to be deliberate and
        // protected, and a pair of dropdowns in the panel is both.
        new SettingRow
        {
            Key = ShipCoreShipKey,
            Label = "Ship",
            Help =
                "Which ship the core below belongs to. Every ship in your fleet is here, so you do "
                + "not have to be sitting in one to give it a core.",
            Kind = SettingKind.Choice,
            DocsAnchor = "core-for-this-ship",

            // What puts a rule between this pair and the persona rows above (remediation.md 16,
            // item 3). Reported as wanting a dividing line under "Forget introductions", and a
            // group is that line plus the one thing a bare rule cannot carry — a name saying what
            // the rows beneath it are for. Both rows name the same group, so it is drawn once.
            Group = "A core per ship",

            // Protected, which is what keeps Phase 35's rule after these stopped being Info rows.
            // Binding a core changes who speaks every time that ship is boarded, so it stays
            // reachable from the panel, the gesture and the model-free router, and is refused to
            // the model outright — d47 reads the Commander's journal and their in-game messages,
            // and other people write those.
            Protected = true,

            // A ship id, which only means something for the Commander whose fleet it counts
            // (list.md Phase 44): one Commander's ship 7 and another's are two ships.
            Scope = SettingScope.Commander,
            ChoiceSource = _ => [.. ships.Fleet().Select(entry => Keyed(entry.ShipId))],
            ChoiceLabel = key => ships.Fleet()
                .FirstOrDefault(entry => Keyed(entry.ShipId) == key)
                .Said ?? key,
            WhyNoChoices = _ =>
                "I have not seen your fleet yet. Dock somewhere with a shipyard and I will read it.",
            Binding = new SettingBinding
            {
                Read = settings => settings.Persona.ShipCoreShip is var id and not 0 ? Keyed(id) : null,
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
            Label = "Core for that ship",
            Help =
                "A ship can remember the core that flies it, so changing ship changes who answers "
                + "you. Nothing is bound until you say so, and boarding a ship you have not bound "
                + "leaves whoever is aboard aboard. Choosing nobody takes a binding back.",
            Kind = SettingKind.Choice,
            DocsAnchor = "core-for-this-ship",
            Protected = true,

            // Per Commander through the store rather than the settings file: what it reads is the
            // binding for the ship the row above points at, and ship-cores.json carries the
            // Commander beside each one (list.md Phase 44). Declared here so the row says what
            // the file already does.
            Scope = SettingScope.Commander,

            // The same group as the row above, so the pair sits under one heading and the rule is
            // drawn once rather than between two rows that are one thought.
            Group = "A core per ship",

            // Nobody first, because it is the unbind and it used to be a separate button. One
            // control that can say "not this one" is fewer than two that cannot.
            Choices = [Nobody, .. PersonaCatalog.Shipped.Select(persona => persona.Id)],
            ChoiceSource = _ => [Nobody, .. PersonaCatalog.All.Select(persona => persona.Id)],
            ChoiceLabel = id => id == Nobody
                ? "Nobody — whoever is aboard stays aboard"
                : PersonaCatalog.Resolve(id).Name,
            Binding = new SettingBinding
            {
                Read = settings => settings.Persona.ShipCoreShip is var id and not 0
                    ? ships.For(id)?.Core ?? Nobody
                    : null,

                // The write is the binding, and it returns the settings unchanged: what it changes
                // lives in ship-cores.json, not here. Contained to this one row, and the reason is
                // in D47Settings.ShipCoreShip — widening SettingBinding for it would have been an
                // architecture change for one control.
                Write = (settings, value) =>
                {
                    if (settings.Persona.ShipCoreShip is var id and not 0)
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
            Label = "Cores by ship",
            Help =
                "Every ship you have bound, and what it flies with. The file behind it is "
                + "ship-cores.json beside d47.exe, and it is meant to be readable — one line per "
                + "ship, hand-editable, with the hull and the name written beside the id so you "
                + "can tell which ship is which.",
            Kind = SettingKind.Info,
            DocsAnchor = "cores-by-ship",
            PressLabel = "Forget this ship's core",
            Press = () => ships.Forget(),
            Binding = new SettingBinding { Read = _ => ships.DescribeAll() },
        },
    ];

    /// <summary>
    /// What the introductions row states. Names the cores rather than counting them, because
    /// the question a Commander has in front of this button is which ones are spent.
    /// </summary>
    private static string Introductions(PersonaHost host)
    {
        var introduced = host.Introduced;

        return introduced.Count == 0
            ? "No core has introduced itself yet. Every one of them still has its first line waiting."
            : $"Already introduced: {string.Join(", ", introduced.Select(p => p.Name))}. "
              + "Selecting one of those again gets its gap reaction instead.";
    }

    /// <summary>
    /// The closed phrase set that reaches this row without a model. Spelled out per core rather
    /// than matched loosely, for the reason every <see cref="SettingCommandPhrase"/> is: a
    /// router that guesses at values is a router that changes the wrong setting with total
    /// confidence.
    /// </summary>
    /// <summary>The row that opens the editor for the Commander's own cores.</summary>
    public const string OwnKey = "persona.own";

    /// <summary>
    /// What that row reads. Here rather than in the App, so the panel and the tool surface cannot
    /// describe the same set differently.
    /// </summary>
    public static string SummariseOwn()
    {
        var names = Persona.PersonaCatalog.Own?.Invoke().Select(core => core.Name).ToArray() ?? [];

        return names.Length == 0
            ? "None yet. A core needs a name and a paragraph saying what it is like."
            : $"{names.Length} of your own: {string.Join(", ", names)}.";
    }

    private static IEnumerable<SettingCommandPhrase> SelectionPhrases(Persona.Persona persona)
    {
        var name = persona.Name.ToLowerInvariant();

        yield return new SettingCommandPhrase($"switch to {name}", persona.Id);
        yield return new SettingCommandPhrase($"be {name}", persona.Id);
        yield return new SettingCommandPhrase($"become {name}", persona.Id);
        yield return new SettingCommandPhrase($"persona {name}", persona.Id);
        yield return new SettingCommandPhrase($"wake {name}", persona.Id);

        // "The Heretic" and "L-LAM-0" both read badly after "switch to the", and a Commander
        // saying the bare name is the likeliest phrasing for every core. Offered as well as
        // rather than instead of, so both work.
        if (name.StartsWith("the ", StringComparison.Ordinal))
        {
            var bare = name[4..];

            yield return new SettingCommandPhrase($"switch to {bare}", persona.Id);
            yield return new SettingCommandPhrase($"be {bare}", persona.Id);
            yield return new SettingCommandPhrase($"become {bare}", persona.Id);
        }
    }

    /// <summary>
    /// Puts a core aboard, and takes the ship AI's name with it or leaves it behind, according
    /// to the row above.
    /// <para>
    /// Here rather than in the app, because every way of changing core — the panel, the hotkey,
    /// the model-free keyword router — writes this row, and a rule enforced beside one of those
    /// callers is a rule the other two do not follow.
    /// </para>
    /// <para>
    /// Only on an actual change. Writing the core that is already aboard is not a switch, and
    /// stripping the name for it would mean an unrelated settings edit could quietly rename the
    /// Commander's companion.
    /// </para>
    /// </summary>
    private static D47Settings WriteCoreAboard(D47Settings settings, string? value)
    {
        var incoming = PersonaCatalog.Knows(value) ? value! : PersonaCatalog.DefaultId;
        var switching = !string.Equals(incoming, settings.Persona.Id, StringComparison.Ordinal);

        return settings with
        {
            Persona = settings.Persona with
            {
                Id = incoming,
                ShipName = switching && !settings.Persona.KeepShipName ? null : settings.Persona.ShipName,
            },
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Describe(PersonaHost host, D47Settings settings, ShipCoreService? ships)
    {
        var report = new StringBuilder();
        var current = host.Current;

        report.AppendLine($"Active persona: {current.Name} — {current.Tagline}");

        if (!string.Equals(host.ShipName, current.Name, StringComparison.Ordinal))
        {
            report.AppendLine($"The Commander calls you {host.ShipName}.");
        }

        report.AppendLine(settings.Llm.PersonalityEnabled
            ? "Personality is on."
            : "Personality is off, so you are answering plainly and this persona's voice is not in play.");

        // The binding, read and never written (list.md Phase 35). Here rather than in a tool of
        // its own: the model is allowed to know which core the Commander's ship asks for, and an
        // existing tool saying one sentence more costs nothing on the advertised surface.
        if (ships is not null)
        {
            report.AppendLine(ships.DescribeCurrent()
                + " That is the Commander's own binding — you can say what it is, and only they can change it.");
        }

        report.AppendLine();
        report.AppendLine("Available personas (the Commander changes these from the panel or by saying so):");

        foreach (var persona in PersonaCatalog.All)
        {
            report.AppendLine($"  {persona.Id} — {persona.Name}: {persona.Tagline}");
        }

        return report.ToString().TrimEnd();
    }
}
