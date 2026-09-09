using System.Globalization;

namespace D47.Core.Conversation;

/// <summary>How one model id is written in a picker (#152).</summary>
public static class ModelChoice
{
    /// <summary>How this provider's models read right now.</summary>
    /// <param name="endpoint">The address chosen for this provider, or null for its own.</param>
    public static Func<string, string> Describer(
        LlmProviderInfo provider,
        string? endpoint,
        PriceTable prices)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(prices);

        // What the turn will actually connect to.
        var local = LocalEndpoint.IsLoopback(endpoint ?? provider.DefaultEndpoint);
        var listed = provider.ModelsFor(endpoint);
        var cheapest = local ? null : Cheapest(provider.Id, listed, prices);

        return model => Label(provider, model, prices, local, cheapest);
    }

    /// <summary>One row: the id, then whichever of the two derived words apply, then what it costs.</summary>
    private static string Label(
        LlmProviderInfo provider,
        string model,
        PriceTable prices,
        bool local,
        string? cheapest)
    {
        var parts = new List<string> { model };

        if (string.Equals(model, provider.DefaultModel, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("the provider's default");
        }

        if (string.Equals(model, cheapest, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("cheapest here");
        }

        parts.Add(local ? "free on this machine" : Rate(prices.For(provider.Id, model)));

        return string.Join(" — ", parts);
    }

    /// <summary>What a model costs per million tokens, or that d47 cannot say.</summary>
    private static string Rate(ModelPrice? price) =>
        price is null
            ? "priced as unknown"
            : $"{Money(price.InputPerMillion)} in / {Money(price.OutputPerMillion)} out per million";

    /// <summary>
    /// A list price, with the cents only where there are cents. "$2" and "$0.20" both read as prices;
    /// "$2.00" beside "$0.20" reads as a table of figures, and this line is a sentence.
    /// </summary>
    private static string Money(decimal dollars) =>
        dollars == decimal.Truncate(dollars)
            ? dollars.ToString("C0", CultureInfo.CurrentCulture)
            : dollars.ToString("C2", CultureInfo.CurrentCulture);

    /// <summary>The cheapest of the offered ids, or null where the word would not earn its place.</summary>
    private static string? Cheapest(string providerId, IReadOnlyList<string> listed, PriceTable prices)
    {
        if (listed.Count < 2)
        {
            return null;
        }

        return listed
            .Select(model => (Model: model, Price: prices.For(providerId, model)))
            .Where(row => row.Price is not null)
            .OrderBy(row => row.Price!.InputPerMillion)
            .ThenBy(row => row.Price!.OutputPerMillion)
            .Select(row => row.Model)
            .FirstOrDefault();
    }
}
