using System.Globalization;
using System.Text.Json;

using D47.Core.Audio;
using D47.Core.Conversation;

namespace D47.Core.Catalog;

/// <summary>How d47 must call one model, where the provider's API does not say.</summary>
public sealed record ModelTraits
{
    /// <summary>What a model the catalog does not describe is assumed to be.</summary>
    public static ModelTraits Unknown { get; } = new();

    /// <summary>Whether a system message can carry live game state with operator authority.</summary>
    public bool OperatorSystemMessages { get; init; }

    /// <summary>Whether the model takes the tool search tool and <c>defer_loading</c>.</summary>
    public bool ToolSearch { get; init; }

    public int MinimumCacheablePrefix { get; init; } = 1024;

    /// <summary>Whether the model must be given the basic web search tool.</summary>
    public bool BasicWebSearchOnly { get; init; }

    /// <summary>Whether the model rejects <c>thinking</c> and <c>output_config.effort</c>.</summary>
    public bool LegacyThinking { get; init; }
}

/// <summary>One model in the catalog.</summary>
public sealed record CatalogModel(string Id, bool Offered, ModelPrice? Price, ModelTraits? Traits);

/// <summary>One speech model in the catalog, with what it does that its provider's API does not say.</summary>
public sealed record SpeechModel(string Id, string Label, bool Offered)
{
    /// <summary>Null where the provider's list price applies.</summary>
    public decimal? DollarsPerThousandCharacters { get; init; }

    /// <summary>Whether the model honours a speaking rate.</summary>
    public bool ReadsRate { get; init; }

    /// <summary>Whether the model performs bracketed delivery direction rather than reading it aloud.</summary>
    public bool ReadsTags { get; init; }

    /// <summary>How much text the model would rather be handed at once, or zero for one sentence at a time.</summary>
    public int GroupsSentencesUpTo { get; init; }
}

/// <summary>Every model fact d47 needs that a provider's API does not return.</summary>
public sealed class ModelCatalog
{
    public const int Schema = 1;

    private const string ResourceName = "D47.Core.ModelCatalog";

    private static readonly Lazy<ModelCatalog> EmbeddedCatalog = new(ReadEmbedded);

    /// <summary>The speech providers whose requests name a model, each of which the catalog must cover.</summary>
    private static readonly string[] SpeechProvidersWithAModel =
        [TtsProviderCatalog.ElevenLabsId, TtsProviderCatalog.OpenAiId, TtsProviderCatalog.CartesiaId];

    private readonly Dictionary<string, Provider> _providers;

    private readonly Dictionary<string, SpeechProvider> _speech;

    private ModelCatalog(
        DateOnly published,
        Dictionary<string, Provider> providers,
        Dictionary<string, SpeechProvider> speech)
    {
        Published = published;
        _providers = providers;
        _speech = speech;
    }

    /// <summary>The catalog built into this release.</summary>
    public static ModelCatalog Embedded => EmbeddedCatalog.Value;

    public DateOnly Published { get; }

    public string? DefaultFor(string providerId) => Find(providerId)?.Default;

    public string? BackgroundDefaultFor(string providerId) => Find(providerId)?.BackgroundDefault;

    /// <summary>The ids that appear in the picker, in catalog order.</summary>
    public IReadOnlyList<string> OfferedFor(string providerId) => Find(providerId)?.Offered ?? [];

    /// <summary>Null when the model is priced as unknown.</summary>
    public ModelPrice? PriceFor(string providerId, string model) => Model(providerId, model)?.Price;

    public ModelTraits TraitsFor(string providerId, string model) =>
        Model(providerId, model)?.Traits ?? ModelTraits.Unknown;

    /// <summary>Never null for ElevenLabs, OpenAI or Cartesia, which every readable catalog covers.</summary>
    public string? SpeechDefaultFor(string providerId) => _speech.GetValueOrDefault(providerId)?.Default;

    /// <summary>The speech models that appear in the picker, in catalog order.</summary>
    public IReadOnlyList<SpeechModel> OfferedSpeechFor(string providerId) =>
        _speech.GetValueOrDefault(providerId)?.Offered ?? [];

    public SpeechModel? SpeechModelFor(string providerId, string model) =>
        _speech.GetValueOrDefault(providerId)?.Models.GetValueOrDefault(model);

    /// <summary>Reads a catalog, throwing <see cref="FormatException"/> for one d47 cannot use.</summary>
    public static ModelCatalog Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return Read(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new FormatException($"The model catalog is not readable: {ex.Message}", ex);
        }
    }

    private Provider? Find(string providerId) => _providers.GetValueOrDefault(providerId);

    private CatalogModel? Model(string providerId, string model) =>
        Find(providerId)?.Models.GetValueOrDefault(model);

    private static ModelCatalog Read(JsonElement root)
    {
        var schema = root.GetProperty("schema").GetInt32();

        if (schema != Schema)
        {
            throw new FormatException($"The model catalog has schema {schema}; this release reads schema {Schema}.");
        }

        var published = DateOnly.ParseExact(
            root.GetProperty("published").GetString() ?? string.Empty,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        var providers = new Dictionary<string, Provider>(StringComparer.Ordinal);

        foreach (var entry in root.GetProperty("providers").EnumerateObject())
        {
            providers[entry.Name] = ReadProvider(entry.Name, entry.Value);
        }

        var speech = new Dictionary<string, SpeechProvider>(StringComparer.Ordinal);

        foreach (var entry in root.GetProperty("speech").EnumerateObject())
        {
            speech[entry.Name] = ReadSpeechProvider(entry.Name, entry.Value);
        }

        foreach (var providerId in SpeechProvidersWithAModel)
        {
            if (!speech.ContainsKey(providerId))
            {
                throw new FormatException($"The model catalog has no {providerId} speech model.");
            }
        }

        return new ModelCatalog(published, providers, speech);
    }

    private static SpeechProvider ReadSpeechProvider(string providerId, JsonElement element)
    {
        var models = new Dictionary<string, SpeechModel>(StringComparer.Ordinal);
        var offered = new List<SpeechModel>();

        foreach (var item in element.GetProperty("models").EnumerateArray())
        {
            var model = ReadSpeechModel(providerId, item);

            if (!models.TryAdd(model.Id, model))
            {
                throw new FormatException($"The model catalog lists {providerId}/{model.Id} twice.");
            }

            if (model.Offered)
            {
                offered.Add(model);
            }
        }

        var defaultModel = OptionalString(element, "default");

        if (defaultModel is null || !offered.Exists(model => model.Id == defaultModel))
        {
            throw new FormatException(
                $"The model catalog's {providerId} speech default, {defaultModel ?? "missing"}, is not an offered model.");
        }

        return new SpeechProvider(defaultModel, offered, models);
    }

    private static SpeechModel ReadSpeechModel(string providerId, JsonElement item)
    {
        var id = item.GetProperty("id").GetString();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new FormatException($"The model catalog has a {providerId} speech model with no id.");
        }

        return new SpeechModel(id, OptionalString(item, "label") ?? id, item.GetProperty("offered").GetBoolean())
        {
            DollarsPerThousandCharacters =
                item.TryGetProperty("dollarsPerThousandCharacters", out var price) && price.ValueKind is not JsonValueKind.Null
                    ? price.GetDecimal()
                    : null,
            ReadsRate = OptionalBool(item, "readsRate"),
            ReadsTags = OptionalBool(item, "readsTags"),
            GroupsSentencesUpTo = item.TryGetProperty("groupsSentencesUpTo", out var budget) ? budget.GetInt32() : 0,
        };
    }

    private static Provider ReadProvider(string providerId, JsonElement element)
    {
        var models = new Dictionary<string, CatalogModel>(StringComparer.Ordinal);
        var offered = new List<string>();

        foreach (var item in element.GetProperty("models").EnumerateArray())
        {
            var model = ReadModel(providerId, item);

            if (!models.TryAdd(model.Id, model))
            {
                throw new FormatException($"The model catalog lists {providerId}/{model.Id} twice.");
            }

            if (model.Offered)
            {
                offered.Add(model.Id);
            }
        }

        var defaultModel = OptionalString(element, "default");

        if (defaultModel is not null && !offered.Contains(defaultModel, StringComparer.Ordinal))
        {
            throw new FormatException(
                $"The model catalog's {providerId} default, {defaultModel}, is not an offered model.");
        }

        return new Provider(defaultModel, OptionalString(element, "backgroundDefault"), offered, models);
    }

    private static CatalogModel ReadModel(string providerId, JsonElement item)
    {
        var id = item.GetProperty("id").GetString();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new FormatException($"The model catalog has a {providerId} model with no id.");
        }

        var offered = item.GetProperty("offered").GetBoolean();
        var price = item.TryGetProperty("price", out var p) && p.ValueKind is not JsonValueKind.Null
            ? ReadPrice(p)
            : null;

        if (offered && price is null)
        {
            throw new FormatException($"The model catalog offers {providerId}/{id} with no price.");
        }

        var traits = item.TryGetProperty("traits", out var t) && t.ValueKind is not JsonValueKind.Null
            ? ReadTraits(t)
            : null;

        return new CatalogModel(id, offered, price, traits);
    }

    private static ModelPrice ReadPrice(JsonElement element)
    {
        var price = new ModelPrice(
            element.GetProperty("input").GetDecimal(),
            element.GetProperty("output").GetDecimal());

        return price with
        {
            CacheReadFactor = element.TryGetProperty("cacheRead", out var read) ? read.GetDecimal() : price.CacheReadFactor,
            CacheWriteFactor = element.TryGetProperty("cacheWrite", out var write) ? write.GetDecimal() : price.CacheWriteFactor,
        };
    }

    private static ModelTraits ReadTraits(JsonElement element) => new()
    {
        OperatorSystemMessages = OptionalBool(element, "operatorSystemMessages"),
        ToolSearch = OptionalBool(element, "toolSearch"),
        MinimumCacheablePrefix = element.TryGetProperty("minimumCacheablePrefix", out var prefix)
            ? prefix.GetInt32()
            : ModelTraits.Unknown.MinimumCacheablePrefix,
        BasicWebSearchOnly = OptionalBool(element, "basicWebSearchOnly"),
        LegacyThinking = OptionalBool(element, "legacyThinking"),
    };

    private static bool OptionalBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.GetBoolean();

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null
            ? value.GetString()
            : null;

    private static ModelCatalog ReadEmbedded()
    {
        using var stream = typeof(ModelCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The {ResourceName} resource is missing from D47.Core.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    private sealed record Provider(
        string? Default,
        string? BackgroundDefault,
        IReadOnlyList<string> Offered,
        Dictionary<string, CatalogModel> Models);

    private sealed record SpeechProvider(
        string Default,
        IReadOnlyList<SpeechModel> Offered,
        Dictionary<string, SpeechModel> Models);
}
