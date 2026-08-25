namespace D47.Core.Audio;

/// <summary>
/// What one slot is wired to: the provider speaking for it, and whether that provider had its
/// key when its voice list was last asked for.
/// </summary>
public sealed record SpeechSlotWiring(string ProviderId, bool KeyPresent);

/// <summary>
/// What the composition root is currently holding, per slot (list.md Phase 57).
/// <para>
/// <see cref="Nothing"/> is the state before the first apply of the process, and it is not the
/// same as "every slot is on Edge" — the empty map is what makes the first apply build clients
/// rather than deciding nothing has changed.
/// </para>
/// <para>
/// Per slot rather than per provider even though the clients are per provider, because the
/// question this answers is "has this slot moved", and two slots on one provider can move
/// independently of each other.
/// </para>
/// </summary>
public sealed record SpeechWiringState(IReadOnlyDictionary<VoiceGroup, SpeechSlotWiring> Slots)
{
    public static SpeechWiringState Nothing { get; } =
        new(new Dictionary<VoiceGroup, SpeechSlotWiring>());

    public SpeechSlotWiring? Of(VoiceGroup group) => Slots.GetValueOrDefault(group);

    /// <summary>
    /// Every provider a client is held for, each once. "none" is not one of them: a slot that
    /// does not speak holds nothing to dispose.
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
    /// <summary>
    /// Providers to construct a client for. <b>One per provider, never one per slot.</b>
    /// <c>ElevenLabsTtsProvider.MaxConcurrent</c> gates the account rather than the pipeline —
    /// *"Callouts, crew lines and re-voiced comms all share the same account, so the gate has to
    /// be here rather than in any one pipeline"* — and that reasoning only survives if two slots
    /// choosing one provider share one instance. Six clients would each believe they owned the
    /// whole concurrency budget, which is the fault Phase 11 already fixed once: a red banner and
    /// a sentence the Commander never heard.
    /// </summary>
    public required IReadOnlyList<string> Build { get; init; }

    /// <summary>
    /// Providers no slot wants any more, to dispose. A provider still speaking for one slot is
    /// never here, however many other slots left it.
    /// </summary>
    public required IReadOnlyList<string> Dispose { get; init; }

    /// <summary>
    /// Slots whose provider changed, so their voice assignments go: a voice id belongs to the
    /// provider that issued it, and a sender holding one from the provider this slot just left is
    /// a sentence that will fail.
    /// </summary>
    public required IReadOnlyList<VoiceGroup> Rewire { get; init; }

    /// <summary>Providers to ask what they can say. Background and best-effort at the call site.</summary>
    public required IReadOnlyList<string> RefetchVoices { get; init; }

    public required SpeechWiringState Next { get; init; }

    /// <summary>Whether anything at all moved. A settings apply that changed nothing here is common.</summary>
    public bool Anything => Build.Count > 0 || Dispose.Count > 0 || Rewire.Count > 0;
}

/// <summary>
/// When speech clients are built, when they are released and when a voice list is asked for
/// again — the decisions the composition root used to make inline, lifted here because both of
/// the faults found in one afternoon's hand-testing lived in them and neither could be reached
/// by a test (list.md Phase 19, "Give the composition root a test harness").
/// <para>
/// A pure function of what is held and what is selected. It builds nothing, disposes nothing
/// and reaches no network: the root still owns every one of those, and owns them in one place
/// each. What it no longer owns is the question of <em>whether</em>.
/// </para>
/// <para>
/// Phase 57 made it a diff rather than a bool. The property that mattered is the one that did
/// not change — it is still a pure function, so the arithmetic of six slots sharing three
/// clients is assertable without a provider, a key or a sound card.
/// </para>
/// </summary>
public static class SpeechWiring
{
    /// <param name="held">What the root has now. <see cref="SpeechWiringState.Nothing"/> at startup.</param>
    /// <param name="selected">
    /// The provider each slot is to speak through, from <see cref="VoiceGroups.Selected"/>. Ids
    /// are resolved through <see cref="TtsProviderCatalog.Selected"/> here rather than by the
    /// caller, so a settings file naming a provider d47 no longer ships plans the same way the
    /// rest of the app treats it.
    /// </param>
    /// <param name="keyPresent">
    /// Whether a provider's credential is stored, or true where it needs none. The root answers
    /// this because the secret store is the root's, and it is the only input here that is not
    /// settings.
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

            // A provider with nothing behind it — "none" — releases its client without building
            // one, which still has to happen: it is what lets a Commander go quiet.
            Dispose = [.. have.Where(id => !wanted.Contains(id, StringComparer.OrdinalIgnoreCase))],
            Rewire = rewire,

            // A key arriving is the other thing that changes what a provider can tell us, and it
            // does not change the provider. Selecting ElevenLabs before pasting the key fetched
            // an empty list and nothing refetched it, so the picker stayed empty until the app
            // was restarted — with the key sitting in the row above it. This is that fault, and
            // it is an edge rather than a level so that pasting the same key twice is free.
            RefetchVoices =
            [
                .. wanted.Where(id =>
                    build.Contains(id, StringComparer.OrdinalIgnoreCase)
                    || KeyPresence(held, id) != keyPresent(id)),
            ],
            Next = state,
        };
    }

    /// <summary>
    /// Whether a provider had its key last time round. Read off any slot that was on it — the
    /// key belongs to the provider, so every slot sharing one agrees by construction. Null-ish
    /// (false) for a provider nothing was wired to, which never decides anything on its own:
    /// such a provider is in <c>Build</c> and refetches for that reason.
    /// </summary>
    private static bool KeyPresence(SpeechWiringState held, string providerId) =>
        held.Slots.Values.FirstOrDefault(slot =>
            string.Equals(slot.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))?.KeyPresent
        ?? false;
}
