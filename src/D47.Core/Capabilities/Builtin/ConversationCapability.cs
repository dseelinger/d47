using System.Text;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything about which model answers, and on what terms. Its settings rows are the
/// clearest case of "show the controls the active provider actually has" (list.md Phase 4):
/// the endpoint, key and model rows are generated from the provider catalog and each one
/// declares when it applies, so selecting "none" leaves nothing behind but the selector
/// itself rather than a hardwired panel of dead controls.
/// </summary>
public static class ConversationCapability
{
    public const string Id = "conversation";

    public const string ProviderKey = "llm.provider";

    public const string EndpointKey = "llm.endpoint";

    public const string ModelKey = "llm.model";

    /// <summary>The secret row key for a provider's API key. One row per provider that needs one.</summary>
    public static string KeyRowFor(LlmProviderInfo provider) => $"llm.{provider.Id}.apiKey";

    public static CapabilityDescriptor Create(
        SettingsService settings,
        LlmAvailabilityState availability,
        SpendTracker spend)
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
            // Phrases only. "model" and "cost" on their own belong to half the questions a
            // Commander might ask about the game itself.
            Keywords =
            [
                "model status",
                "which model",
                "what model",
                "session cost",
                "session spend",
                "what have you cost",
            ],
            Display = new CapabilityDisplay { PanelTitle = "Language model", Order = 30 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_model_status",
                    Description =
                        "Report the selected language model provider and model, whether it is currently reachable, "
                        + "and this session's token spend so far.",
                    Handler = (_, _) => Task.FromResult(
                        ToolResult.Ok(DescribeModel(settings.Current, availability, spend))),
                },
            ],
            Settings = BuildSettingRows(),
        };
    }

    private static string DescribeModel(
        D47Settings settings,
        LlmAvailabilityState availability,
        SpendTracker spend)
    {
        var provider = LlmProviderCatalog.Selected(settings.Llm.Provider);

        var report = new StringBuilder();
        report.AppendLine($"Provider: {provider.Name}");

        if (provider.Id != LlmProviderCatalog.NoneId)
        {
            report.AppendLine($"Model: {settings.Llm.Model ?? provider.DefaultModel ?? "(provider default)"}");
            report.AppendLine($"Endpoint: {settings.Llm.Endpoint ?? provider.DefaultEndpoint ?? "(provider default)"}");
        }

        report.AppendLine($"Availability: {availability.Current}{(availability.Reason is { } why ? $" — {why}" : "")}");
        report.AppendLine($"Personality: {(settings.Llm.PersonalityEnabled ? "on" : "off")}");
        report.AppendLine(
            $"Session so far: {spend.TurnCount} turn(s), {spend.RunningTotalDollars:C4}");

        return report.ToString().TrimEnd();
    }

    private static IReadOnlyList<SettingRow> BuildSettingRows()
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = ProviderKey,
                Label = "Provider",
                Help = "Which language model answers. \"None\" keeps every turn on this machine.",
                Kind = SettingKind.Choice,
                Choices = LlmProviderCatalog.Ids,
                ChoiceLabel = id => LlmProviderCatalog.Selected(id).Name,
                DocsAnchor = "provider",
                // Protected: choosing a provider chooses where your turns go. A model that can
                // point itself at another endpoint is a model that can be told to, by text it
                // read in an in-game message (architecture.md §7).
                Protected = true,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Provider,
                    // Endpoint and model both belong to the provider's namespace, so neither
                    // survives the switch. Carrying one across is how a stale selection happens.
                    Write = (s, v) => s with
                    {
                        Llm = s.Llm with
                        {
                            Provider = v ?? LlmProviderCatalog.AnthropicId,
                            Endpoint = null,
                            Model = null,
                        },
                    },
                },
            },
            new()
            {
                Key = EndpointKey,
                Label = "Endpoint",
                Help = "Point at something else speaking the same protocol. Clearing it restores the provider's own.",
                Kind = SettingKind.Text,
                DefaultDisplaySource = s => LlmProviderCatalog.Selected(s.Llm.Provider).DefaultEndpoint,
                DocsAnchor = "endpoint",
                Protected = true,
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).HasEndpoint,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Endpoint,
                    // Changing the endpoint resets the model list to that endpoint's namespace
                    // rather than leaving a stale selection (list.md Phase 4).
                    Write = (s, v) => s with { Llm = s.Llm with { Endpoint = v, Model = null } },
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
                // A custom endpoint has models d47 has never heard of, so the list can be empty
                // and a typed value has to be accepted (list.md Phase 4, the picker's contract).
                AllowsFreeText = true,
                ChoiceSource = s => LlmProviderCatalog.Selected(s.Llm.Provider).ModelsFor(s.Llm.Endpoint),
                AppliesWhen = s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
                Binding = new SettingBinding
                {
                    Read = s => s.Llm.Model,
                    Write = (s, v) => s with { Llm = s.Llm with { Model = v } },
                },
            },
        };

        // One key row per provider that needs one, rather than a single row whose secret name
        // shifts underneath it. Each declares when it applies, so only the selected provider's
        // key is on screen.
        rows.AddRange(
            from provider in LlmProviderCatalog.All
            where provider.NeedsKey
            select new SettingRow
            {
                Key = KeyRowFor(provider),
                Label = $"{provider.Name} API key",
                Help = "Stored encrypted for this Windows account. Write-only: d47 will never show it back to you.",
                Kind = SettingKind.Secret,
                SecretName = provider.KeySecretName,
                DocsAnchor = "api-key",
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
            // Fixed phrases, fixed values, no interpretation — the shape every protected row's
            // voice path will take. Variants are spelled out because the alternative is the
            // router deciding for itself which words were filler.
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
            Key = "llm.aboutMe",
            Label = "About Me",
            Help = "Standing context about you, sent with every turn. Kept between sessions.",
            Kind = SettingKind.Text,
            Multiline = true,
            DefaultDisplay = "(nothing yet)",
            DocsAnchor = "about-me",
            Binding = new SettingBinding
            {
                Read = s => s.Llm.AboutMe,
                Write = (s, v) => s with { Llm = s.Llm with { AboutMe = v } },
            },
        });

        return rows;
    }
}
