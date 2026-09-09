using System.Text;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Everything about which model answers, and on what terms.</summary>
public static class ConversationCapability
{
    public const string Id = "conversation";

    public const string ProviderKey = "llm.provider";

    public const string EndpointKey = "llm.endpoint";

    public const string ModelKey = "llm.model";

    /// <summary>The model for calls the Commander is not waiting on (Phase 54).</summary>
    public const string BackgroundModelKey = "llm.backgroundModel";

    public const string EffortFloorKey = "llm.effortFloor";

    public const string EffortCeilingKey = "llm.effortCeiling";

    /// <summary>Whether the model may search the web.</summary>
    public const string WebSearchKey = "llm.webSearch";

    /// <summary>The secret row key for a provider's API key.</summary>
    public static string KeyRowFor(LlmProviderInfo provider) => $"llm.{provider.Id}.apiKey";

    /// <summary>
    /// The live half of web search: a line for prompt position 7 when d47 cannot look anything up, and
    /// nothing at all when it can.
    /// </summary>
    /// <param name="enabled">The Commander's <see cref="WebSearchKey"/> setting.</param>
    /// <param name="available">
    /// What <see cref="LlmProviderCapabilities.SupportsWebSearch"/> says for the provider and model in
    /// use.
    /// </param>
    public static string? LiveSearch(bool enabled, bool available)
    {
        if (!available)
        {
            return "D47 cannot look anything up online: the language-model endpoint in use offers no web "
                   + "search. Say so plainly rather than answering a question that needs current "
                   + "information as though it had been checked. Anthropic's own endpoint offers search; "
                   + "a gateway or a local model may not.";
        }

        return enabled
            ? null
            : "D47 cannot look anything up online: the Commander has web search switched off. Say so "
              + "plainly rather than answering a question that needs current information as though it "
              + "had been checked. They can turn it on in Settings, under the language model.";
    }

    /// <summary>
    /// <param name="verifyKey"> Tries a provider's stored key against the real service, by provider id
    /// (Phase 16).
    /// </summary>
    /// <param name="verifyKey">
    /// Tries a provider's stored key against the real service, by provider id (Phase 16).
    /// </param>
    public static CapabilityDescriptor Create(
        SettingsService settings,
        LlmAvailabilityState availability,
        SpendTracker spend,
        TurnCancellation cancellation,
        Action silence,
        Func<string, CancellationToken, Task<SecretCheck>>? verifyKey = null,
        Func<Audio.SpeechSpend?>? speechSpend = null,
        Func<IReadOnlyList<string>>? endpointModels = null)
    {
        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Conversation",
            Name = "Language model",
            Summary = "Report which model is answering, whether it is reachable, and what this session has cost.",
            Examples =
            [
                "which model are you using",
                "what has this session cost",
                "turn your personality off",
            ],
            // Phrases only. "model" and "cost" on their own belong to half the questions a Commander might
            // ask about the game itself. Each names its tool, and here the positional pick had already
            // gone wrong (#161). cancel_turn is declared first and takes no argument, so every phrase
            // below — six questions about which model is running and what it has cost — *cancelled the turn*
            // instead of answering.
            Keywords =
            [
                new("model status", "get_model_status"),
                new("which model", "get_model_status"),
                new("what model", "get_model_status"),
                new("session cost", "get_model_status"),
                new("session spend", "get_model_status"),
                new("what have you cost", "get_model_status"),
            ],

            // "Cancel" is as common a verb as "stop" and is kept out of the general vocabulary for the same
            // reason.
            InterruptKeywords = ["cancel", "cancel that", "never mind", "nevermind", "forget it"],
            Display = new CapabilityDisplay { PanelTitle = "Language model", Order = 1 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "cancel_turn",
                    Description =
                        "Abandon the turn currently running: stop speaking, stop the model, and stop " +
                        "spending. Use when the Commander says to cancel or never mind.",

                    // Stronger than stop_speaking and correspondingly rarer.
                    Interrupting = true,
                    Handler = (_, _) =>
                    {
                        // Silence first.
                        silence();

                        return Task.FromResult(cancellation.Cancel()
                            ? ToolResult.Ok("Cancelled.")
                            : ToolResult.Ok("Nothing was running to cancel."));
                    },
                },
                new ToolDefinition
                {
                    Name = "get_model_status",
                    Description =
                        "Report the selected language model provider and model, whether it is currently reachable, "
                        + "and this session's token spend so far.",
                    Handler = (_, _) => Task.FromResult(
                        ToolResult.Ok(DescribeModel(settings.Current, availability, spend, speechSpend))),
                },
            ],
            Settings = BuildSettingRows(verifyKey, endpointModels),
        };
    }

    private static string DescribeModel(
        D47Settings settings,
        LlmAvailabilityState availability,
        SpendTracker spend,
        Func<Audio.SpeechSpend?>? speechSpend)
    {
        var provider = LlmProviderCatalog.Selected(settings.Llm.Provider);

        var report = new StringBuilder();
        report.AppendLine($"Provider: {provider.Name}");

        if (provider.Id != LlmProviderCatalog.NoneId)
        {
            report.AppendLine($"Model: {settings.Llm.Model ?? provider.DefaultModel ?? "(provider default)"}");

            // Only when the Commander has split the two (Phase 54), following the rule the endpoint line
            // below already uses: a line saying the quiet calls use the model that was named one line above
            // tells them nothing they did not just read.
            if (settings.Llm.BackgroundModel is { Length: > 0 } background)
            {
                report.AppendLine($"Model for the quiet calls: {background}");
            }

            // Only when it is not the provider's own. "Endpoint: .anthropic.com" is a line that tells the
            // Commander where Anthropic is, which they knew.
            if (settings.Llm.Endpoint is { Length: > 0 } chosen)
            {
                report.AppendLine($"Endpoint: {chosen}");
            }
        }

        report.AppendLine($"Availability: {availability.Current}{(availability.Reason is { } why ? $" — {why}" : "")}");
        report.AppendLine($"Personality: {(settings.Llm.PersonalityEnabled ? "on" : "off")}");
        report.AppendLine(
            $"Session so far: {spend.TurnCount} response(s), {spend.RunningTotalDollars:C4}");

        // Here rather than on a capability of its own, because the question a Commander asks is "what has
        // this cost" and it has one answer (Phase 19).
        if (speechSpend?.Invoke()?.Describe(settings) is { } voice)
        {
            report.AppendLine($"Speech so far: {voice}");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// How both model rows write a choice: the id, then its price, then whichever of the provider's
    /// default and cheapest here apply (#152).
    /// </summary>
    /// <param name="endpointModels">
    /// What the endpoint itself said it serves, when d47 has asked it (Phase 29).
    /// </param>
    private static Func<string, string> Describer(D47Settings settings) =>
        ModelChoice.Describer(
            LlmProviderCatalog.Selected(settings.Llm.Provider),
            settings.Llm.Endpoint,
            PriceTable.Default);

    private static IReadOnlyList<SettingRow> BuildSettingRows(
        Func<string, CancellationToken, Task<SecretCheck>>? verifyKey,
        Func<IReadOnlyList<string>>? endpointModels = null)
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = ProviderKey,
                Label = "Provider",
                Help = "Which language model answers. \"None\" keeps every response on this machine.",
                Kind = SettingKind.Choice,
                Choices = LlmProviderCatalog.Ids,
                ChoiceLabel = id => LlmProviderCatalog.Selected(id).Name,
                DocsAnchor = "provider",
                // Protected: choosing a provider chooses where your turns go.
                Protected = true,

                // Protected rows are unreachable from the tool surface, so without phrases here the Commander
                // cannot switch provider by voice at all.
                Commands =
                [
                    new SettingCommandPhrase("stop using the language model", LlmProviderCatalog.NoneId),
                    new SettingCommandPhrase("turn off the language model", LlmProviderCatalog.NoneId),
                    new SettingCommandPhrase("go local only", LlmProviderCatalog.NoneId),
                    new SettingCommandPhrase("use anthropic", LlmProviderCatalog.AnthropicId),
                    new SettingCommandPhrase("turn on the language model", LlmProviderCatalog.AnthropicId),
                    new SettingCommandPhrase("use openai", LlmProviderCatalog.OpenAiId),
                    new SettingCommandPhrase("use open ai", LlmProviderCatalog.OpenAiId),

                    // Said the way a Commander would say it.
                    new SettingCommandPhrase("use my own model", LlmProviderCatalog.OpenAiCompatibleId),
                    new SettingCommandPhrase("use my local model", LlmProviderCatalog.OpenAiCompatibleId),
                ],
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Provider,
                    // Endpoint and model both belong to the provider's namespace, so neither survives the
                    // switch.
                    Write = (s, v) => s with
                    {
                        Llm = s.Llm with
                        {
                            Provider = v ?? LlmProviderCatalog.AnthropicId,
                            Endpoint = null,

                            // Both models, and the second one is the quiet half: a background model left over
                            // from another provider is a request that fails where nothing is watching —
                            // FlavourTurn logs at Debug and returns null, so every ambient line falls back to
                            // its authored text with nothing on screen (Phase 54).
                            Model = null,
                            BackgroundModel = null,
                        },
                    },
                },
            },
            new()
            {
                Key = EndpointKey,
                Advanced = true,
                Label = "Endpoint",
                Help = "Point at something else speaking the same protocol. Clearing it restores the provider's own.",
                Kind = SettingKind.Text,
                DefaultDisplaySource = s => LlmProviderCatalog.Selected(s.Llm.Provider).DefaultEndpoint,
                DocsAnchor = "endpoint",
                Protected = true,
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).AcceptsCustomEndpoint,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Endpoint,
                    // Changing the endpoint resets the model list to that endpoint's namespace rather than
                    // leaving a stale selection (Phase 4) — both models, for the reason the provider row
                    // above states.
                    Write = (s, v) => s with
                    {
                        Llm = s.Llm with { Endpoint = v, Model = null, BackgroundModel = null },
                    },
                },
            },
            new()
            {
                Key = ModelKey,
                Label = "Model",
                Help = "Which model at that endpoint. Leave it unset to use the provider's default.",
                Kind = SettingKind.Choice,
                DefaultDisplaySource = s => LlmProviderCatalog.Selected(s.Llm.Provider).DefaultModel,
                DocsAnchor = "model",
                // A custom endpoint has models d47 has never heard of, so the list can be empty and a typed
                // value has to be accepted (Phase 4, the picker's contract).
                AllowsFreeText = true,

                // The provider's own list where it has one, and the endpoint's own where it does not (Phase
                // 29).
                ChoiceSource = s =>
                {
                    var known = LlmProviderCatalog.Selected(s.Llm.Provider).ModelsFor(s.Llm.Endpoint);

                    return known.Count > 0 ? known : endpointModels?.Invoke() ?? [];
                },

                // What each one costs, which is the fact the row is chosen on and the one it never said
                // (#152).
                ChoiceLabelSource = Describer,
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Model,
                    Write = (s, v) => s with { Llm = s.Llm with { Model = v } },
                },
            },
            new()
            {
                Key = BackgroundModelKey,
                Advanced = true,
                Label = "Model for the quiet calls",
                Help =
                    "Which model writes the things you did not ask for — In Ship chatter, the opening "
                    + "brief, what D47 says after a long gap, a lore lookup, and choosing a voice. None "
                    + "of them carry the conversation, so a cheaper model here costs nothing in cache "
                    + "and saves most of what D47 spends when you are not talking to it. Leave it unset "
                    + "and they use the model above.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the conversation model)",
                DocsAnchor = "background-model",

                // The model row's contract, for the model row's reason: an endpoint d47 has never heard of
                // still has model names (Phase 4).
                AllowsFreeText = true,
                ChoiceSource = s =>
                {
                    var known = LlmProviderCatalog.Selected(s.Llm.Provider).ModelsFor(s.Llm.Endpoint);

                    return known.Count > 0 ? known : endpointModels?.Invoke() ?? [];
                },

                // The same words as the row above, from the same source.
                ChoiceLabelSource = Describer,
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,

                // No voice commands, and not for want of a phrase.
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.BackgroundModel,
                    Write = (s, v) => s with { Llm = s.Llm with { BackgroundModel = v } },
                },
            },
            new()
            {
                Key = EffortFloorKey,
                Advanced = true,
                Label = "Think at least this hard",
                Help =
                    "The least effort a question gets, however plain it looked. D47 gauges each question "
                    + "on its own and spends the cheapest setting that answers it; this is where you say "
                    + "the cheapest setting is not enough.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(gauged per question)",
                DocsAnchor = "effort-floor",

                // Both, and both are needed.
                Choices = ThinkingEffortRange.Names,
                ChoiceSource = s => s.Llm.EffortCeiling is { } ceiling
                    ? ThinkingEffortRange.NamesUpTo(ceiling)
                    : ThinkingEffortRange.Names,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.EffortFloor is { } floor ? ThinkingEffortRange.Name(floor) : null,
                    Write = (s, v) => s with { Llm = s.Llm with { EffortFloor = ThinkingEffortRange.Parse(v) } },
                },
            },
            new()
            {
                Key = EffortCeilingKey,
                Advanced = true,
                Label = "Never think harder than this",
                Help =
                    "The most effort a question gets, however hard it sounded. Thinking is what a turn "
                    + "mostly costs, so this is the dial that decides what a heavy question is allowed to "
                    + "spend — and it catches the times D47 reads a passing remark as a request to "
                    + "deliberate.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(gauged per question)",
                DocsAnchor = "effort-ceiling",
                Choices = ThinkingEffortRange.Names,
                ChoiceSource = s => s.Llm.EffortFloor is { } floor
                    ? ThinkingEffortRange.NamesFrom(floor)
                    : ThinkingEffortRange.Names,

                // Said the way a Commander would say it, and each maps to a fixed rung from the closed ladder
                // rather than to anything extracted from the words.
                Commands =
                [
                    new SettingCommandPhrase(
                        "stop thinking so hard",
                        ThinkingEffortRange.Name(ThinkingEffort.Medium)),
                    new SettingCommandPhrase("think as hard as you like", null),
                ],
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.EffortCeiling is { } ceiling ? ThinkingEffortRange.Name(ceiling) : null,
                    Write = (s, v) => s with { Llm = s.Llm with { EffortCeiling = ThinkingEffortRange.Parse(v) } },
                },
            },
        };

        // One key row per provider that has one, rather than a single row whose secret name shifts underneath
        // it.
        rows.AddRange(
            from provider in LlmProviderCatalog.All
            where provider.AcceptsKey
            select new SettingRow
            {
                Key = KeyRowFor(provider),
                Label = provider.KeyOptional ? $"{provider.Name} API key (if it wants one)" : $"{provider.Name} API key",
                Help = provider.KeyOptional
                    ? "Optional — a model running on this machine needs no key, and leaving this empty is a "
                      + "complete configuration. Fill it in only if the endpoint asks for one. Stored encrypted "
                      + "for this Windows account, and write-only: D47 will never show it back to you."
                    : "Stored encrypted for this Windows account. Write-only: D47 will never show it back to you.",
                Kind = SettingKind.Secret,
                SecretName = provider.KeySecretName,
                DocsAnchor = "api-key",
                EgressId = EgressDisclosure.LanguageModel,

                // The one key that decides whether d47 can answer at all, so it is the one worth proving
                // before the Commander closes the window and finds out on the first turn.
                Verify = verifyKey is { } verify
                    ? token => verify(provider.Id, token)
                    : null,
                AppliesWhen = s => string.Equals(s.Llm.Provider, provider.Id, StringComparison.OrdinalIgnoreCase),
            });

        rows.Add(new SettingRow
        {
            Key = "llm.personality",
            Label = "Personality",
            Help = "Off gives plain answers. The anti-invention guardrails are unaffected either way.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "on",
            DocsAnchor = "personality",
            // Fixed phrases, fixed values, no interpretation — the shape every protected row's voice path
            // will take.
            Commands =
            [
                new SettingCommandPhrase("personality off", "false"),
                new SettingCommandPhrase("turn personality off", "false"),
                new SettingCommandPhrase("turn your personality off", "false"),
                new SettingCommandPhrase("personality on", "true"),
                new SettingCommandPhrase("turn personality on", "true"),
                new SettingCommandPhrase("turn your personality on", "true"),
            ],
            Binding = new SettingBinding
            {
                Read = s => s.Llm.PersonalityEnabled ? "true" : "false",
                Write = (s, v) => s with { Llm = s.Llm with { PersonalityEnabled = v is not "false" } },
            },
        });

        rows.Add(new SettingRow
        {
            Key = WebSearchKey,
            Label = "Let the model search the web",
            Help =
                "Lets D47 look something up online when a question needs current information — a "
                + "community guide, patch notes, what other Commanders are reporting. The search runs "
                + "at your language-model provider, not here, so nothing new leaves this machine "
                + "beyond what you already send them. What comes back is spoken as something D47 read, "
                + "never written into its own tables. Off by default; see [Privacy](privacy) for what is sent. "
                + "Searches are billed separately by the provider, about a penny each.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "off",
            DocsAnchor = "let-the-model-search-the-web",
            Binding = new SettingBinding
            {
                Read = s => s.Llm.WebSearch ? "true" : "false",
                Write = (s, v) => s with { Llm = s.Llm with { WebSearch = v == "true" } },
            },
        });

        // The two halves of a biography, in the order they are read (Phase 43).
        rows.Add(new SettingRow
        {
            Key = "llm.characterSheet",
            Advanced = true,
            Label = "Character sheet",
            Help =
                "Who your Commander is, in a few lines — name, where they are from, age, how they "
                + "speak. D47 carries this with every turn and with everything it says in character, "
                + "so it knows whose ship this is. Keep it short; the story goes below.",
            Kind = SettingKind.Text,
            Multiline = true,
            DefaultDisplay = "(nothing yet)",
            DocsAnchor = "character-sheet",

            // The Commander's, in exactly the way About Me is (Phase 44): whose ship this is changes when who
            // is flying changes.
            Scope = SettingScope.Commander,
            Binding = new SettingBinding
            {
                Read = s => s.Llm.CharacterSheet,
                Write = (s, v) => s with { Llm = s.Llm with { CharacterSheet = v } },
            },
        });

        rows.Add(new SettingRow
        {
            Key = "llm.aboutMe",
            Advanced = true,
            Label = "About Me",
            Help =
                "Your Commander's story, in your own words — as long as you like. D47 treats it as true "
                + "of the world you share. Sent with every turn, and with about one unprompted remark in "
                + "four, so the ship's AI knows who it is flying with. Kept between sessions.",
            Kind = SettingKind.Text,
            Multiline = true,
            DefaultDisplay = "(nothing yet)",
            DocsAnchor = "about-me",

            // The most per-Commander thing in the file, and until Phase 44 in the least per-Commander place.
            Scope = SettingScope.Commander,
            Binding = new SettingBinding
            {
                Read = s => s.Llm.AboutMe,
                Write = (s, v) => s with { Llm = s.Llm with { AboutMe = v } },
            },
        });

        return rows;
    }
}
