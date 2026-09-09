namespace D47.Core.Audio;

/// <summary>
/// What one slot is wired to: the provider speaking for it, and whether that provider had its key when
/// its voice list was last asked for.
/// </summary>
public sealed record SpeechSlotWiring(string ProviderId, bool KeyPresent);

/// <summary>What the composition root is currently holding, per slot (Phase 57).</summary>
public sealed record SpeechWiringState(IReadOnlyDictionary<VoiceGroup, SpeechSlotWiring> Slots)
{
    public static SpeechWiringState Nothing { get; } =
        new(new Dictionary<VoiceGroup, SpeechSlotWiring>());

    public SpeechSlotWiring? Of(VoiceGroup group) => Slots.GetValueOrDefault(group);

    /// <summary>
    /// Every provider a client is held for, each once. "none" is not one of them: a slot that does not
    /// speak holds nothing to dispose.
    /// </summary>
    public IReadOnlyList<string> Providers =>
        [.. Slots.Values
            .Select(slot => slot.ProviderId)
            .Where(id => TtsProviderCatalog.Selected(id).Speaks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)];
}

/// <summary>What to do about it, and what the root will be holding once it has.</summary>
public sealed record SpeechWiringPlan
{
    /// <summary>Providers to construct a client for.</summary>
    public required IReadOnlyList<string> Build { get; init; }

    /// <summary>Providers no slot wants any more, to dispose.</summary>
    public required IReadOnlyList<string> Dispose { get; init; }

    /// <summary>
    /// Slots whose provider changed, so their voice assignments go: a voice id belongs to the provider
    /// that issued it, and a sender holding one from the provider this slot just left is a sentence
    /// that will fail.
    /// </summary>
    public required IReadOnlyList<VoiceGroup> Rewire { get; init; }

    /// <summary>Providers to ask what they can say.</summary>
    public required IReadOnlyList<string> RefetchVoices { get; init; }

    public required SpeechWiringState Next { get; init; }

    /// <summary>Whether anything at all moved.</summary>
    public bool Anything => Build.Count > 0 || Dispose.Count > 0 || Rewire.Count > 0;
}

/// <summary>
/// When speech clients are built, when they are released and when a voice list is asked for again — the
/// composition root's speech decisions, here so a test can reach them.
/// </summary>
public static class SpeechWiring
{
    /// <summary><param name="held">What the root has now.</summary>
    /// <param name="held">What the root has now.</param>
    /// <param name="selected">
    /// The provider each slot is to speak through, from <see cref="VoiceGroups.Selected"/>.
    /// </param>
    /// <param name="keyPresent">
    /// Whether a provider's credential is stored, or true where it needs none.
    /// </param>
    public static SpeechWiringPlan Plan(
        SpeechWiringState held,
        IReadOnlyDictionary<VoiceGroup, string> selected,
        Func<string, bool> keyPresent)
    {
        var next = new Dictionary<VoiceGroup, SpeechSlotWiring>();
        var rewire = new List<VoiceGroup>();

        foreach (var slot in VoiceGroups.All)
        {
            var provider = TtsProviderCatalog.Selected(selected.GetValueOrDefault(slot.Group));
            next[slot.Group] = new SpeechSlotWiring(provider.Id, keyPresent(provider.Id));

            if (held.Of(slot.Group) is not { } was
                || !string.Equals(was.ProviderId, provider.Id, StringComparison.Ordinal))
            {
                rewire.Add(slot.Group);
            }
        }

        var state = new SpeechWiringState(next);
        var wanted = state.Providers;
        var have = held.Providers;

        var build = wanted.Where(id => !have.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();

        return new SpeechWiringPlan
        {
            Build = build,

            // A provider with nothing behind it — "none" — releases its client without building one, which
            // still has to happen: it is what lets a Commander go quiet.
            Dispose = [.. have.Where(id => !wanted.Contains(id, StringComparer.OrdinalIgnoreCase))],
            Rewire = rewire,

            // A key arriving is the other thing that changes what a provider can tell us, and it does not
            // change the provider.
            RefetchVoices =
            [
                .. wanted.Where(id =>
                    build.Contains(id, StringComparer.OrdinalIgnoreCase)
                    || KeyPresence(held, id) != keyPresent(id)),
            ],
            Next = state,
        };
    }

    /// <summary>Whether a provider had its key last time round.</summary>
    private static bool KeyPresence(SpeechWiringState held, string providerId) =>
        held.Slots.Values.FirstOrDefault(slot =>
            string.Equals(slot.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))?.KeyPresent
        ?? false;
}
