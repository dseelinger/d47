namespace D47.Core.Audio;

/// <summary>
/// What the composition root is currently holding: which provider it built a client for, and
/// whether that provider had its key when the voice list was last asked for.
/// <para>
/// <see cref="Nothing"/> is the state before the first apply of the process, and it is not the
/// same as "Edge is selected" — the null is what makes the first apply build a client rather
/// than deciding nothing has changed.
/// </para>
/// </summary>
public sealed record SpeechWiringState(string? ProviderId, bool KeyPresent)
{
    public static readonly SpeechWiringState Nothing = new(ProviderId: null, KeyPresent: false);
}

/// <summary>What to do about it, and what the root will be holding once it has.</summary>
public sealed record SpeechWiringPlan
{
    /// <summary>
    /// Dispose whatever client is held, drop the voice list and the sender assignments, and
    /// build the selected provider's client. A voice id belongs to the provider that issued it,
    /// so none of those three survive a switch.
    /// </summary>
    public required bool RebuildClient { get; init; }

    /// <summary>Ask the provider what it can say. Background and best-effort at the call site.</summary>
    public required bool RefetchVoices { get; init; }

    public required SpeechWiringState Next { get; init; }
}

/// <summary>
/// When the speech client is rebuilt and when the voice list is asked for again — the two
/// decisions the composition root used to make inline, lifted here because both of the faults
/// found in one afternoon's hand-testing lived in them and neither could be reached by a test
/// (list.md Phase 19, "Give the composition root a test harness").
/// <para>
/// A pure function of what is held and what is selected. It builds nothing, disposes nothing
/// and reaches no network: the root still owns every one of those, and owns them in one place
/// each. What it no longer owns is the question of <em>whether</em>.
/// </para>
/// </summary>
public static class SpeechWiring
{
    /// <param name="held">What the root has now. <see cref="SpeechWiringState.Nothing"/> at startup.</param>
    /// <param name="selectedProviderId">
    /// The provider id from settings. Resolved through <see cref="TtsProviderCatalog.Selected"/>
    /// here rather than by the caller, so a settings file naming a provider d47 no longer ships
    /// plans the same way the rest of the app treats it.
    /// </param>
    /// <param name="keyPresent">
    /// Whether the selected provider's credential is stored, or true where it needs none. The
    /// root answers this because the secret store is the root's, and it is the only input here
    /// that is not settings.
    /// </param>
    public static SpeechWiringPlan Plan(SpeechWiringState held, string? selectedProviderId, bool keyPresent)
    {
        var provider = TtsProviderCatalog.Selected(selectedProviderId);
        var next = new SpeechWiringState(provider.Id, keyPresent);

        if (!string.Equals(held.ProviderId, provider.Id, StringComparison.Ordinal))
        {
            // A provider with nothing behind it — "none" — is a rebuild to no client at all,
            // which still has to happen: it is what releases the previous one.
            return new SpeechWiringPlan
            {
                RebuildClient = true,
                RefetchVoices = provider.Speaks,
                Next = next,
            };
        }

        return new SpeechWiringPlan
        {
            RebuildClient = false,

            // A key arriving is the other thing that changes what a provider can tell us, and it
            // does not change the provider. Selecting ElevenLabs before pasting the key fetched
            // an empty list and nothing refetched it, so the picker stayed empty until the app
            // was restarted — with the key sitting in the row above it. This is that fault, and
            // it is an edge rather than a level so that pasting the same key twice is free.
            RefetchVoices = provider.Speaks && keyPresent != held.KeyPresent,
            Next = next,
        };
    }
}
