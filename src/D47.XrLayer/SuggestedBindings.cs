namespace D47.XrLayer;

/// <summary>
/// The bindings Elite last suggested for each interaction profile. A suggestion replaces the earlier one for its
/// profile, so the layer's own suggestion carries Elite's with it.
/// </summary>
internal sealed unsafe class SuggestedBindings
{
    /// <summary>The most bindings kept from one suggestion.</summary>
    public const int MostBindings = 1024;

    private readonly Lock gate = new();
    private readonly Dictionary<(ulong Instance, ulong Profile), XrActionSuggestedBinding[]?> byProfile = [];

    /// <summary>Keeps a copy of a suggestion the runtime accepted.</summary>
    public void Record(ulong instance, XrInteractionProfileSuggestedBinding* suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        var count = suggestion->CountSuggestedBindings;
        XrActionSuggestedBinding[]? copy = null;
        if (count <= MostBindings && (count == 0 || suggestion->SuggestedBindings is not null))
        {
            copy = new ReadOnlySpan<XrActionSuggestedBinding>(suggestion->SuggestedBindings, (int)count).ToArray();
        }

        lock (gate)
        {
            byProfile[(instance, suggestion->InteractionProfile)] = copy;
        }
    }

    /// <summary>Elite's last suggestion for the profile: empty when it made none, null when it was too large to keep.</summary>
    public XrActionSuggestedBinding[]? For(ulong instance, ulong profile)
    {
        lock (gate)
        {
            return byProfile.TryGetValue((instance, profile), out var bindings) ? bindings : [];
        }
    }
}
