using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace D47.Core.Stories;

/// <summary>A stock story's public layer: what the Commander picks it by.</summary>
public sealed record StoryCard
{
    public required string Id { get; init; }

    public int Number { get; init; }

    public required string Title { get; init; }

    public string? Tone { get; init; }

    /// <summary>The Commander's backstory in the first person; picking the story makes it their Backstory.</summary>
    public required string InYourWords { get; init; }

    public string? Heart { get; init; }

    public string? DrawsOn { get; init; }

    /// <summary>Why this Commander goes to a Guardian beacon.</summary>
    public string? Beacon { get; init; }

    public string? PlaysThrough { get; init; }

    /// <summary>What d47 must track before the story is offered, or null. See <see cref="StoryCatalog.Tracked"/>.</summary>
    public string? Requires { get; init; }

    /// <summary>The layer as the model reads it.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine(Tone is { Length: > 0 } tone ? $"{Title} — {tone}." : $"{Title}.");
        text.AppendLine($"In the Commander's words: \"{InYourWords}\"");
        Line(text, "Heart", Heart);
        Line(text, "Draws on", DrawsOn);
        Line(text, "The beacon", Beacon);
        Line(text, "Plays through", PlaysThrough);
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

/// <summary>A stock story's hidden layer. Sent to the model; never shown or logged.</summary>
public sealed record StorySecret
{
    public required string Id { get; init; }

    public required string Secret { get; init; }

    public required string Weeks { get; init; }

    public required string Months { get; init; }

    public required string Year { get; init; }

    public required string End { get; init; }
}

/// <summary>
/// The stock stories: <c>StoryCatalog.json</c> (public) and <c>StoryCatalog.sealed</c> (hidden, raw deflate then
/// base64), both embedded. <c>tools/seal-stories.py</c> decodes and re-encodes the hidden layer.
/// </summary>
public sealed class StoryCatalog
{
    public const string PublicResource = "D47.Core.StoryCatalog";

    public const string SealedResource = "D47.Core.StoryCatalog.Sealed";

    /// <summary>The requirements d47 meets. A card whose <see cref="StoryCard.Requires"/> is not here is not offered.</summary>
    public static readonly IReadOnlySet<string> Tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Every card, in catalog order, offered or not.</summary>
    public IReadOnlyList<StoryCard> Cards { get; }

    /// <summary>The cards the Stories page lists.</summary>
    public IReadOnlyList<StoryCard> Offered => [.. Cards.Where(card => card.Requires is null || Tracked.Contains(card.Requires))];

    public StoryCard? Find(string? id) =>
        Cards.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The hidden layer of one story, decoded on first use.</summary>
    public StorySecret? Secret(string id) => _secrets.Value.GetValueOrDefault(id);

    /// <summary>Every hidden entry, for the gate that checks each has a card.</summary>
    public IReadOnlyCollection<StorySecret> Secrets => [.. _secrets.Value.Values];

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
