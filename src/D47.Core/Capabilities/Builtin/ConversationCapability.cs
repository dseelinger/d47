using System.Text;
using D47.Core.Catalog;
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

    public const string TestKey = "llm.test";

    /// <summary>The model for calls the Commander is not waiting on (Phase 54).</summary>
    public const string BackgroundModelKey = "llm.backgroundModel";
    public const string HighModelKey = "llm.highModel";

    public const string EffortFloorKey = "llm.effortFloor";

    public const string EffortCeilingKey = "llm.effortCeiling";

    /// <summary>Whether the model may search the web.</summary>
    public const string WebSearchKey = "llm.webSearch";

    /// <summary>Whether the model may take a picture of the screen.</summary>
    public const string LookAtScreenKey = "llm.lookAtScreen";

    /// <summary>Whether the published model catalog is fetched.</summary>
    public const string RefreshCatalogKey = "models.refreshCatalog";

    private const string ScenarioAboard = "aboard";

    private const string ScenarioCarrier = "carrier";

    private const string ScenarioPublic = "public";

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

    /// <summary>The look-at-screen row's line for a model that cannot read a picture.</summary>
    public static string PictureNote(string model) => $"{model} does not read pictures, so D47 never takes one.";

    /// <summary>The model row's line for a model whose context is known to be too small for every tool (#423).</summary>
    public static string ContextNote(int contextTokens, int offered, int total) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"This model's context is {contextTokens:N0} tokens, so D47 offers it {offered} of {total} tools. "
            + $"Set its context length to {contextTokens * 2:N0} or more in your server for all of them.");

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
        Func<IReadOnlyList<string>>? endpointModels = null,
        Func<string?>? contextNote = null,
        Func<string?>? pictureNote = null)
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
                    Exercise =
                        "While D47 is working on a model reply, say \"never mind\" or \"cancel that\". "
                        + "Speech stops, the turn is abandoned and the reply is \"Cancelled.\" With "
                        + "nothing running it is \"Nothing was running to cancel.\"",
                    AlwaysLoaded = true,
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
                    Exercise =
                        "Say \"which model are you using\". The reply names the provider, the model, "
                        + "whether the provider is reachable, whether personality is on, and this "
                        + "session's response count and cost. A separate background or high model "
                        + "adds a line.",
                    Description =
                        "Report the selected language model provider and model, whether it is currently reachable, "
                        + "and this session's token spend so far.",
                    Handler = (_, _) => Task.FromResult(
                        ToolResult.Ok(DescribeModel(settings.Current, availability, spend, speechSpend))),
                },
            ],
            Settings = BuildSettingRows(settings, verifyKey, availability, endpointModels, contextNote, pictureNote),
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
            // below already uses: a line saying the background model is the model that was named one line above
            // tells them nothing they did not just read.
            if (settings.Llm.BackgroundModel is { Length: > 0 } background)
            {
                report.AppendLine($"Background model: {background}");
            }

            if (settings.Llm.HighModel is { Length: > 0 } high)
            {
                report.AppendLine($"High model: {high}");
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
        SettingsService settings,
        Func<string, CancellationToken, Task<SecretCheck>>? verifyKey,
        LlmAvailabilityState availability,
        Func<IReadOnlyList<string>>? endpointModels = null,
        Func<string?>? contextNote = null,
        Func<string?>? pictureNote = null)
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = ProviderKey,
                Exercise =
                    "Open Settings › Providers › Language model, group Provider and model, and find Provider. Choose "
                    + "None (local only): D47 answers only what it understands when you type and says so when it "
                    + "cannot, nothing is sent to a language model, and the Model row disappears. Choose Anthropic: "
                    + "that provider's API key row appears below. Choosing a provider clears the Endpoint, Model, "
                    + "Background model and Model for complex tasks rows back to their defaults. Say \"go local only\":"
                    + " the row shows None (local only). Say \"use anthropic\", \"use openai\" or \"use my own model\":"
                    + " the row shows Anthropic, OpenAI or OpenAI-compatible endpoint. A real answer needs a key stored"
                    + " for the chosen provider (or a local server for the OpenAI-compatible endpoint).",
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
                            HighModel = null,
                        },
                    },
                },
            },
            new()
            {
                Key = EndpointKey,
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "Open Settings › Providers › Language model, group Provider and model, and find Endpoint. It "
                    + "appears only with OpenAI or OpenAI-compatible endpoint chosen as Provider, and is absent for "
                    + "Anthropic and None (local only). It shows the provider's own address as the default. Type "
                    + "another address, such as a local server's, and the Model, Background model and Model for complex"
                    + " tasks rows reset to unset. Clear the field and the provider's own address returns, with the "
                    + "same reset. Needs a server speaking the same protocol at that address. It has no voice phrase.",
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
                        Llm = s.Llm with { Endpoint = v, Model = null, BackgroundModel = null, HighModel = null },
                    },
                },
            },
            new()
            {
                Key = ModelKey,
                Exercise =
                    "Open Settings › Providers › Language model, group Provider and model, and find Model. It is "
                    + "absent while Provider is None (local only). The default shows the provider's default model. Open"
                    + " the list and choose another: each entry carries its description and cost, and the row shows the"
                    + " choice. On OpenAI or the OpenAI-compatible endpoint a model name can also be typed. Clear it to"
                    + " return to the default. The next answer comes from the chosen model.",
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
                Note = s => s.Llm.Provider == LlmProviderCatalog.OpenAiCompatibleId ? contextNote?.Invoke() : null,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Model,
                    Write = (s, v) => s with { Llm = s.Llm with { Model = v } },
                },
            },
            new()
            {
                Key = TestKey,
                Label = "Test",
                Help =
                    "Asks the selected provider for its list of models, with the stored key. Free: it runs no "
                    + "completion and adds nothing to this session's spend. A working answer marks the model "
                    + "available; a refusal or silence marks it unavailable.",
                Kind = SettingKind.Info,
                DocsAnchor = "test",
                PressLabel = "Test",
                PressAsync = verifyKey is null
                    ? null
                    : async (_, token) =>
                    {
                        var provider = LlmProviderCatalog.Selected(settings.Current.Llm.Provider);
                        var check = await verifyKey(provider.Id, token).ConfigureAwait(false);

                        switch (check.Verdict)
                        {
                            case SecretVerdict.Works:
                                availability.MarkAvailable();
                                break;
                            case SecretVerdict.Rejected:
                                availability.MarkFailed(check.Detail, transient: false);
                                break;
                            default:
                                availability.MarkFailed(check.Detail, transient: true);
                                break;
                        }

                        return check.Detail;
                    },
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
                Binding = new SettingBinding
                {
                    Read = _ => availability.Reason is { } why
                        ? $"{availability.Current} — {why}"
                        : availability.Current.ToString(),
                },
            },
            new()
            {
                Key = BackgroundModelKey,
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "Open Settings › Providers › Language model, group Provider and model, and find Background model."
                    + " It is absent while Provider is None (local only). The default shows the provider's background "
                    + "model, or \"(the conversation model)\" where none is named. Choose another model: the row shows "
                    + "it, and what D47 says unprompted, such as In Ship chatter and the opening brief, is written by "
                    + "that model. Clear it to return to the default. It has no voice phrase.",
                Advanced = true,
                Label = "Background model",
                Help =
                    "Which model writes the things you did not ask for — In Ship chatter, the opening "
                    + "brief, what D47 says after a long gap, a lore lookup, and choosing a voice. None "
                    + "of them carry the conversation, so a cheaper model here costs nothing in cache "
                    + "and saves most of what D47 spends when you are not talking to it. Leave it unset "
                    + "and they use the model named as the default, or the model above where none is named.",
                Kind = SettingKind.Choice,
                DefaultDisplaySource = s => BackgroundModels.DefaultFor(s) is { } named
                    ? ModelCatalogSource.Shared.Current.LabelFor(s.Llm.Provider, named)
                    : "(the conversation model)",
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
                Key = HighModelKey,
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "Open Settings › Providers › Language model, group Provider and model, and find Model for complex"
                    + " tasks. It is absent while Provider is None (local only). The default shows the provider's high-"
                    + "effort model, or \"(the conversation model)\" where none is named. Choose another model: the row"
                    + " shows it, and Talk through this build on a ship's page is answered by that model at high "
                    + "effort. Clear it to return to the default.",
                Advanced = true,
                Label = "Model for complex tasks",
                Help =
                    "Answers Talk through this build on a ship's page, at high effort. A proposed change has "
                    + "to fit the hull's slots, the engineering blueprints and the cost, so choose a strong "
                    + "model; a weak one proposes changes that are wrong or refused. On a custom endpoint, "
                    + "set it here, or build talk uses the conversation model.",
                Kind = SettingKind.Choice,
                DefaultDisplaySource = s => HighModels.DefaultFor(s) is { } named
                    ? ModelCatalogSource.Shared.Current.LabelFor(s.Llm.Provider, named)
                    : "(the conversation model)",
                DocsAnchor = "high-model",
                AllowsFreeText = true,
                ChoiceSource = s =>
                {
                    var known = LlmProviderCatalog.Selected(s.Llm.Provider).ModelsFor(s.Llm.Endpoint);

                    return known.Count > 0 ? known : endpointModels?.Invoke() ?? [];
                },
                ChoiceLabelSource = Describer,
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.HighModel,
                    Write = (s, v) => s with { Llm = s.Llm with { HighModel = v } },
                },
            },
            new()
            {
                Key = EffortFloorKey,
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "Open Settings › Providers › Language model, group Provider and model, and find Think at least "
                    + "this hard. It shows \"(gauged per question)\" by default and offers Low, Medium, High, Xhigh and"
                    + " Max. Choose High: the row reads High. Then open Never think harder than this: its list starts "
                    + "at High. Clear the row to return to \"(gauged per question)\". It has no voice phrase.",
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
                Exercise =
                    "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                    + "Open Settings › Providers › Language model, group Provider and model, and find Never think "
                    + "harder than this. It shows \"(gauged per question)\" by default and offers Low, Medium, High, "
                    + "Xhigh and Max. Choose Medium: the row reads Medium, and the list in Think at least this hard "
                    + "stops at Medium. Say \"stop thinking so hard\": the row reads Medium. Say \"think as hard as you"
                    + " like\": the row returns to \"(gauged per question)\".",
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

        rows.Add(new SettingRow
        {
            Key = RefreshCatalogKey,
            Exercise =
                "Open Settings › Providers › Language model, group Provider and model, and find Fetch model "
                + "updates (on by default). With it on, D47 requests the published model list at startup and every "
                + "24 hours, and Privacy and egress shows the Model updates entry as active. Turn it off: that "
                + "entry reads \"Fetch model updates is off, so nothing is requested\" and D47 uses the model list "
                + "it already has. Say \"stop fetching model updates\": the row shows off. Say \"start fetching "
                + "model updates\": the row shows on. Needs a network connection to GitHub to see a fetch.",
            Label = "Fetch model updates",
            Help =
                "Fetches the published list of models, their defaults and their prices from GitHub at "
                + "startup and every 24 hours, so a new model can be offered without a new release. "
                + "Off uses the list D47 already has.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "on",
            DocsAnchor = "fetch-model-updates",
            EgressId = EgressDisclosure.ModelUpdates,

            // Protected: it decides whether a request leaves.
            Protected = true,
            Commands =
            [
                new SettingCommandPhrase("stop fetching model updates", "false"),
                new SettingCommandPhrase("turn off model updates", "false"),
                new SettingCommandPhrase("start fetching model updates", "true"),
                new SettingCommandPhrase("turn on model updates", "true"),
            ],
            Binding = SettingBinding.Toggle(
                s => s.Models.RefreshCatalog,
                (s, on) => s with { Models = s.Models with { RefreshCatalog = on } }),
        });

        // One key row per provider that has one, rather than a single row whose secret name shifts underneath
        // it.
        rows.AddRange(
            from provider in LlmProviderCatalog.All
            where provider.AcceptsKey
            select new SettingRow
            {
                Key = KeyRowFor(provider),
                Exercise =
                    $"Open Settings › Providers › Language model, group Provider and model, set Provider to "
                    + $"{provider.Name}, and find the API key row for it. "
                    + "The row is absent for any other provider. It reads No key until one is stored. Paste a key into the "
                    + "row only and press SAVE: it reads Key stored. Press "
                    + "VERIFY: a result line says whether the provider accepted the key. Press FORGET KEY and confirm "
                    + "Delete key: it reads No key again. "
                    + (provider.KeyOptional
                        ? "A local server needs no key, so leaving the row empty is a complete setup. "
                        : "Needs a key from the provider's own account. ")
                    + "Do not paste a key anywhere but this row.",
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
            Exercise =
                "Open Settings › Your core › Core and character, group Core and character, and find Personality "
                + "(on by default). Turn it off: answers are plain, without the core's character, and the "
                + "guardrails against invented facts still apply. Say \"personality off\" (or \"turn your "
                + "personality off\"): the row shows off. Say \"personality on\" (or \"turn your personality on\"):"
                + " the row shows on. Needs a language model to see the difference.",
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
            Binding = SettingBinding.Toggle(
                s => s.Llm.PersonalityEnabled,
                (s, on) => s with { Llm = s.Llm with { PersonalityEnabled = on } }),
        });

        rows.Add(new SettingRow
        {
            Key = WebSearchKey,
            Exercise =
                "Open Settings › Providers › Outside sources, group Outside sources, and find Let the model "
                + "search the web (off by default). With it off, ask a question that needs current information, "
                + "such as the latest patch notes: D47 is told to say plainly that it cannot look anything up "
                + "online. Turn it on with a provider whose endpoint offers search, such as Anthropic, and ask "
                + "again: the answer is given as something D47 read. With an endpoint that offers no search, D47 "
                + "still says it cannot look anything up. Needs a language model key; searches are billed by the "
                + "provider. It has no voice phrase.",
            Label = "Let the model search the web",
            Help =
                "Lets D47 look something up online when a question needs current information — a "
                + "community guide, patch notes, what other Commanders are reporting. The search runs "
                + "at your language-model endpoint (which may be xAI), not here, so nothing new leaves this machine "
                + "beyond what you already send them. What comes back is spoken as something D47 read, "
                + "never written into its own tables. Off by default; see [Privacy](privacy) for what is sent. "
                + "Searches are billed separately by the provider, about a penny each.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "off",
            DocsAnchor = "let-the-model-search-the-web",
            Binding = SettingBinding.Toggle(
                s => s.Llm.WebSearch,
                (s, on) => s with { Llm = s.Llm with { WebSearch = on } }),
        });

        rows.Add(new SettingRow
        {
            Key = LookAtScreenKey,
            Exercise =
                "Open Settings › Providers › Outside sources, group Outside sources, and find Let the model look "
                + "at the screen (off by default). Turn it on, with Elite running on a model that reads pictures, "
                + "and ask about something on screen: D47 takes one picture for that question and answers from it, "
                + "and the turn's cost includes the picture. With a model that does not read pictures, the row "
                + "shows a note that it never takes one. Turn it off, or say \"turn off screen pictures\" or \"stop"
                + " looking at my screen\": no picture is taken. Say \"turn on screen pictures\" or \"allow screen "
                + "pictures\": the row shows on. Needs a language model key and the game window or headset view.",
            Label = "Let the model look at the screen",
            Help =
                "Lets D47 take a picture of what you are looking at when you ask about something on screen — "
                + "Elite's window, or the headset's view when Elite is running in VR — and send it to your "
                + "language-model provider with the question. The picture is not saved, and it is sent only for "
                + "that question. A picture adds about 1,100 to 1,600 input tokens to the turn, which the turn's "
                + "cost includes. Off by default; see [Privacy](privacy) for what is sent.",
            Kind = SettingKind.Toggle,
            DefaultDisplay = "off",
            DocsAnchor = "let-the-model-look-at-the-screen",
            EgressId = EgressDisclosure.Screen,

            // Protected: it decides whether a picture leaves, and text in a picture must not be able to turn it on.
            Protected = true,
            Commands =
            [
                new SettingCommandPhrase("turn on screen pictures", "true"),
                new SettingCommandPhrase("allow screen pictures", "true"),
                new SettingCommandPhrase("turn off screen pictures", "false"),
                new SettingCommandPhrase("stop looking at my screen", "false"),
            ],
            Note = s => s.Llm.LookAtScreen ? pictureNote?.Invoke() : null,
            Binding = SettingBinding.Toggle(
                s => s.Llm.LookAtScreen,
                (s, on) => s with { Llm = s.Llm with { LookAtScreen = on } }),
        });

        // The two halves of a biography, in the order they are read (Phase 43).
        rows.Add(new SettingRow
        {
            Key = "llm.characterSheet",
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "Open Settings › You and your records › About you, group About you, and find Character sheet. It "
                + "shows \"(nothing yet)\" until text is entered. Type a few lines, such as a name, where from and "
                + "how the Commander speaks: the row keeps them. Ask D47 who you are, or wait for an in-character "
                + "remark: it uses the sheet. Clear the text to return to \"(nothing yet)\". The text is kept per "
                + "Commander. Needs a language model to see it used.",
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
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "Open Settings › You and your records › About you, group About you, and find Backstory. It shows "
                + "\"(nothing yet)\" until text is entered. Type a backstory of any length: the row keeps it "
                + "between sessions. Ask D47 about your Commander's past: it treats the text as true. Clear the "
                + "text to return to \"(nothing yet)\". The text is kept per Commander. Needs a language model to "
                + "see it used.",
            Advanced = true,
            Label = "Backstory",
            Help =
                "Your Commander's backstory, in your own words — as long as you like. D47 treats it as true "
                + "of the world you share. Sent with every turn, and with about one unprompted remark in "
                + "four, so the core knows who it is flying with. Kept between sessions.",
            Kind = SettingKind.Text,
            Multiline = true,
            DefaultDisplay = "(nothing yet)",
            DocsAnchor = "backstory",

            // The most per-Commander thing in the file, and until Phase 44 in the least per-Commander place.
            Scope = SettingScope.Commander,
            Binding = new SettingBinding
            {
                Read = s => s.Llm.AboutMe,
                Write = (s, v) => s with { Llm = s.Llm with { AboutMe = v } },
            },
        });

        rows.Add(new SettingRow
        {
            Key = "llm.scenario",
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "Open Settings › You and your records › About you, group About you, and find Current scenario. It"
                + " shows \"(nothing yet)\" until text is entered. Type a situation, such as a secret cargo run: "
                + "the row keeps it until cleared. Ask D47 about it: whoever Who knows about it allows treats it as"
                + " true. Clear the text to return to \"(nothing yet)\". The text is kept per Commander. Needs a "
                + "language model to see it used.",
            Advanced = true,
            Label = "Current scenario",
            Help =
                "The situation your Commander is in now — a secret cargo run, a refugee convoy, a debt "
                + "being worked off. D47 treats it as true, and tells it only to those the row below "
                + "says know about it. Kept between sessions until you clear it.",
            Kind = SettingKind.Text,
            Multiline = true,
            DefaultDisplay = "(nothing yet)",
            DocsAnchor = "current-scenario",
            Scope = SettingScope.Commander,
            Binding = new SettingBinding
            {
                Read = s => s.Llm.Scenario,
                Write = (s, v) => s with { Llm = s.Llm with { Scenario = v } },
            },
        });

        rows.Add(new SettingRow
        {
            Key = "llm.scenarioAudience",
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "Open Settings › You and your records › About you, group About you, and find Who knows about it, "
                + "shown under Current scenario. It reads Only those aboard by default and offers Aboard and my "
                + "carrier and Everyone. With a scenario entered, choose Everyone: comms characters can refer to it"
                + " too. Choose Only those aboard: only the core and crew do, though scene chatter at a settlement "
                + "or in a ship fight is given the scenario whatever this is set to. The row keeps the choice. "
                + "Needs a language model to see the difference.",
            Advanced = true,
            Label = "Who knows about it",
            Help =
                "Who hears your scenario: only those aboard — the core and the crew; those aboard and "
                + "your carrier's captain and tower; or everyone, comms included. Scene chatter at a settlement "
                + "or in a ship fight is given it whatever this is set to.",
            Kind = SettingKind.Choice,
            Choices = [ScenarioAboard, ScenarioCarrier, ScenarioPublic],
            ChoiceLabel = id => id switch
            {
                ScenarioCarrier => "Aboard and my carrier",
                ScenarioPublic => "Everyone",
                _ => "Only those aboard",
            },
            DefaultDisplay = ScenarioAboard,
            DocsAnchor = "who-knows-about-it",
            Scope = SettingScope.Commander,
            Binding = new SettingBinding
            {
                Read = s => s.Llm.ScenarioAudience switch
                {
                    ScenarioAudience.Carrier => ScenarioCarrier,
                    ScenarioAudience.Public => ScenarioPublic,
                    _ => ScenarioAboard,
                },
                Write = (s, v) => s with
                {
                    Llm = s.Llm with
                    {
                        ScenarioAudience = v switch
                        {
                            ScenarioCarrier => ScenarioAudience.Carrier,
                            ScenarioPublic => ScenarioAudience.Public,
                            _ => ScenarioAudience.Aboard,
                        },
                    },
                },
            },
        });

        return rows;
    }
}
