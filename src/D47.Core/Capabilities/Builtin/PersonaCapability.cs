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
            Choices = [.. PersonaCatalog.All.Select(p => p.Id)],
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
            Commands = [.. PersonaCatalog.All.SelectMany(SelectionPhrases)],
            Binding = new SettingBinding
            {
                Read = s => s.Persona.Id,
                Write = (s, v) => s with
                {
                    Persona = s.Persona with { Id = PersonaCatalog.Knows(v) ? v! : PersonaCatalog.DefaultId },
                },
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
    ];

    /// <summary>
    /// The closed phrase set that reaches this row without a model. Spelled out per core rather
    /// than matched loosely, for the reason every <see cref="SettingCommandPhrase"/> is: a
    /// router that guesses at values is a router that changes the wrong setting with total
    /// confidence.
    /// </summary>
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
