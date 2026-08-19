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

    public static CapabilityDescriptor Create(PersonaHost host, SettingsService settings) => new()
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
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(host, settings.Current))),
            },
        ],
        Settings = Rows(host),
    };

    private static IReadOnlyList<SettingRow> Rows(PersonaHost host) =>
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

    private static string Describe(PersonaHost host, D47Settings settings)
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

        report.AppendLine();
        report.AppendLine("Available personas (the Commander changes these from the panel or by saying so):");

        foreach (var persona in PersonaCatalog.All)
        {
            report.AppendLine($"  {persona.Id} — {persona.Name}: {persona.Tagline}");
        }

        return report.ToString().TrimEnd();
    }
}
