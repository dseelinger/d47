using System.Globalization;

namespace D47.Core.Conversation;

/// <summary>
/// How one model id is written in a picker
/// (<a href="https://github.com/dseelinger/d47/issues/152">#152</a>).
/// <para>
/// <b>The row used to be the id and nothing else.</b> Five ids reading
/// <c>gpt-5.6-terra / sol / luna / 5.5 / 5.4-mini</c> are a version ladder and no more: a
/// Commander cannot tell the flagship from the budget tier, and the only fact that matters at
/// that row — what does each cost, and which is the sensible default — was one type away in
/// <see cref="PriceTable"/>, unshown. It is why the missing nano tier
/// (<a href="https://github.com/dseelinger/d47/issues/151">#151</a>) took a session to notice:
/// the cheapest listed model was on screen the whole time, indistinguishable from its expensive
/// siblings.
/// </para>
/// <para>
/// <b>Everything here is derived, and that is the constraint rather than a convenience.</b> The
/// price is read from the same table the spend dialog and <c>TurnLoop</c> bill against, so the
/// picker and the invoice cannot disagree; <em>the provider's default</em> is
/// <see cref="LlmProviderInfo.DefaultModel"/>; <em>cheapest here</em> is a minimum over the ids
/// actually offered. None of it is prose about a model, because prose ages silently — capability
/// claims drift and models get renamed under their tiers, while a derived fact ages into
/// wrongness slowly and visibly.
/// </para>
/// </summary>
public static class ModelChoice
{
    /// <summary>
    /// How this provider's models read right now. Built once per picker rather than per row,
    /// because <em>cheapest here</em> is a property of the whole list and would otherwise be
    /// recomputed for every line of it.
    /// </summary>
    /// <param name="endpoint">
    /// The address chosen for this provider, or null for its own. It decides two things: which
    /// ids count as "here" — <see cref="LlmProviderInfo.ModelsFor"/> answers nothing for an
    /// address d47 does not recognise, so a discovered id gets no relative word — and whether the
    /// models run on this machine, which is priced by the address rather than by the id
    /// (<see cref="PriceTable.Free"/>).
    /// </param>
    public static Func<string, string> Describer(
        LlmProviderInfo provider,
        string? endpoint,
        PriceTable prices)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(prices);

        // What the turn will actually connect to. A settings file holds null for "the provider's
        // own", and it is the provider's own address that is loopback for a local server.
        var local = LocalEndpoint.IsLoopback(endpoint ?? provider.DefaultEndpoint);
        var listed = provider.ModelsFor(endpoint);
        var cheapest = local ? null : Cheapest(provider.Id, listed, prices);

        return model => Label(provider, model, prices, local, cheapest);
    }

    /// <summary>
    /// One row: the id, then whichever of the two derived words apply, then what it costs.
    /// <para>
    /// The id comes first and is never dropped. It is what the settings file holds, what a
    /// support answer names, and what a Commander typing into the picker's filter box is
    /// matching against.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What a model costs per million tokens, or that d47 cannot say.
    /// <para>
    /// <b>An id with no row says so rather than saying nothing</b>, which is the same rule the
    /// running total already follows: a model quietly labelled with no price reads as free, and
    /// that is the one wrong answer worth ruling out. A custom endpoint's whole list arrives
    /// here, and it is right that all of it does — those ids belong to somebody else's namespace
    /// and d47 has no published rates for them.
    /// </para>
    /// </summary>
    private static string Rate(ModelPrice? price) =>
        price is null
            ? "priced as unknown"
            : $"{Money(price.InputPerMillion)} in / {Money(price.OutputPerMillion)} out per million";

    /// <summary>
    /// A list price, with the cents only where there are cents. "$2" and "$0.20" both read as
    /// prices; "$2.00" beside "$0.20" reads as a table of figures, and this line is a sentence.
    /// </summary>
    private static string Money(decimal dollars) =>
        dollars == decimal.Truncate(dollars)
            ? dollars.ToString("C0", CultureInfo.CurrentCulture)
            : dollars.ToString("C2", CultureInfo.CurrentCulture);

    /// <summary>
    /// The cheapest of the offered ids, or null where the word would not earn its place.
    /// <para>
    /// By input rate first and output rate second, which is a stated rule rather than a
    /// preference: the two OpenAI tiers at $0.20 in are separated only by their output rates, and
    /// a comparison that stopped at the first number would mark both or neither.
    /// </para>
    /// <para>
    /// Null for a list of one — everything on a one-item list is the cheapest thing on it — and
    /// for a list whose prices d47 does not hold, where the marker would be a claim about models
    /// it cannot compare.
    /// </para>
    /// </summary>
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
