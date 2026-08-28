using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The composition root's speech wiring, driven as a sequence rather than as one call
/// (Phase 19, "Give the composition root a test harness").
/// <para>
/// <c>AppHost</c> is where the app is actually assembled and nothing constructs one, so every
/// behaviour that lived in it was covered only by running the app. Two of the three faults found
/// in one afternoon's hand-testing were in here, and both are below as regression tests —
/// <see cref="AKeyArrivingAfterTheProviderRefetchesTheList"/> and, on the settings half,
/// <c>VoicesAreRememberedPerProviderTests</c>.
/// </para>
/// <para>
/// This is the harness the item asks for and not the one it does not: nothing here mocks an
/// audio device, a network or a logger. The root still builds the client, still owns the secret
/// store and still starts the fetch. What was lifted is the decision, which is a pure function
/// of what is held and what is selected — and which is the part that was wrong twice.
/// </para>
/// <para>
/// Phase 57 turned one bool into a diff over six slots, and the property that mattered is the one
/// that did not change: this is still arithmetic, so "two slots share one client and a third
/// leaving disposes nothing" is assertable without a provider, a key or a sound card.
/// </para>
/// </summary>
public class TheRootDecidesFromSettingsAloneTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;
    private const string None = TtsProviderCatalog.NoneId;

    /// <summary>Every slot on one provider, which is every settings file written before Phase 57.</summary>
    private static IReadOnlyDictionary<VoiceGroup, string> Everywhere(string? provider) =>
        VoiceGroups.All.ToDictionary(slot => slot.Group, _ => provider!);

    /// <summary>The ship on one provider and everybody else on another, which is the phase's default.</summary>
    private static IReadOnlyDictionary<VoiceGroup, string> Split(string aboard, string overTheAir) =>
        VoiceGroups.All.ToDictionary(
            slot => slot.Group,
            slot => slot.Group == VoiceGroup.Aboard ? aboard : overTheAir);

    private static Func<string, bool> Keys(bool present) => _ => present;

    /// <summary>What the root would be holding after one apply of these settings.</summary>
    private static SpeechWiringState Holding(IReadOnlyDictionary<VoiceGroup, string> selected, bool key = true) =>
        SpeechWiring.Plan(SpeechWiringState.Nothing, selected, Keys(key)).Next;

    [Fact]
    public void TheFirstApplyOfTheProcessBuildsAClient()
    {
        var plan = SpeechWiring.Plan(SpeechWiringState.Nothing, Everywhere(Edge), Keys(true));

        // Nothing held is not "Edge held". A root that treated it as such would start with the
        // default provider selected and no client behind it, which is silence on launch.
        Assert.Equal([Edge], plan.Build);
        Assert.Equal([Edge], plan.RefetchVoices);
        Assert.Equal(Edge, plan.Next.Of(VoiceGroup.Aboard)?.ProviderId);
    }

    [Fact]
    public void ApplyingTheSameSettingsTwiceRebuildsNothing()
    {
        var second = SpeechWiring.Plan(Holding(Everywhere(Edge)), Everywhere(Edge), Keys(true));

        // Settings are applied on every change of any row, so this is the common case by a wide
        // margin. Rebuilding here would drop the voice list and the sender assignments every
        // time the Commander moved a slider.
        Assert.False(second.Anything);
        Assert.Empty(second.RefetchVoices);
    }

    [Fact]
    public void ChangingProviderRebuildsAndRefetches()
    {
        var plan = SpeechWiring.Plan(Holding(Everywhere(Edge)), Everywhere(Eleven), Keys(false));

        Assert.Equal([Eleven], plan.Build);
        Assert.Equal([Edge], plan.Dispose);
        Assert.Equal([Eleven], plan.RefetchVoices);
        Assert.Equal(Eleven, plan.Next.Of(VoiceGroup.Aboard)?.ProviderId);
    }

    /// <summary>
    /// One of the two faults. Selecting ElevenLabs before pasting the key fetched an empty list
    /// and nothing refetched it, so the picker stayed empty until the app was restarted — with
    /// the key sitting in the row above it.
    /// </summary>
    [Fact]
    public void AKeyArrivingAfterTheProviderRefetchesTheList()
    {
        var chosenWithNoKey = SpeechWiring.Plan(
            Holding(Everywhere(Edge)), Everywhere(Eleven), Keys(false));

        var keyPasted = SpeechWiring.Plan(chosenWithNoKey.Next, Everywhere(Eleven), Keys(true));

        Assert.Equal([Eleven], keyPasted.RefetchVoices);

        // And it is a refetch, not a rebuild. The client asks for the key per call, so replacing
        // it would throw away a list that had only just arrived.
        Assert.Empty(keyPasted.Build);
        Assert.False(keyPasted.Anything);
    }

    [Fact]
    public void AKeyBeingClearedRefetchesToo()
    {
        // The other direction, and it matters for the same reason: the list on screen was
        // fetched with a key that is no longer stored, so offering it invites choosing a voice
        // that cannot be spoken.
        var plan = SpeechWiring.Plan(Holding(Everywhere(Eleven)), Everywhere(Eleven), Keys(false));

        Assert.Equal([Eleven], plan.RefetchVoices);
    }

    [Fact]
    public void PastingTheSameKeyTwiceCostsNothing()
    {
        var plan = SpeechWiring.Plan(Holding(Everywhere(Eleven)), Everywhere(Eleven), Keys(true));

        Assert.Empty(plan.RefetchVoices);
    }

    /// <summary>
    /// "None" is a supported choice, not an absence of one: d47 stays fully usable in text with
    /// the cues still audible. The release still has to happen — it is what lets the Commander
    /// actually go quiet — and there is nothing to ask for a voice list.
    /// </summary>
    [Fact]
    public void SelectingNoProviderStillReleasesTheOneThatWasSpeaking()
    {
        var plan = SpeechWiring.Plan(Holding(Everywhere(Eleven)), Everywhere(None), Keys(true));

        Assert.Equal([Eleven], plan.Dispose);
        Assert.Empty(plan.Build);
        Assert.Empty(plan.RefetchVoices);
    }

    [Fact]
    public void NoProviderSelectedAsksForNothingOnEveryLaterApply()
    {
        var first = SpeechWiring.Plan(SpeechWiringState.Nothing, Everywhere(None), Keys(false));

        Assert.Empty(SpeechWiring.Plan(first.Next, Everywhere(None), Keys(true)).RefetchVoices);
    }

    /// <summary>
    /// A settings file naming a provider d47 no longer ships resolves to Edge everywhere else in
    /// the app, and has to resolve to Edge here too — a plan keyed on the raw string would
    /// rebuild on every single apply, because the held id and the selected one could never match.
    /// </summary>
    [Fact]
    public void AProviderD47DoesNotShipIsPlannedAsTheOneItResolvesTo()
    {
        var first = SpeechWiring.Plan(SpeechWiringState.Nothing, Everywhere("festival"), Keys(false));

        Assert.Equal(Edge, first.Next.Of(VoiceGroup.Aboard)?.ProviderId);
        Assert.False(SpeechWiring.Plan(first.Next, Everywhere("festival"), Keys(false)).Anything);
    }

    [Fact]
    public void ANullProviderIsTheDefaultRatherThanNoProvider()
    {
        var plan = SpeechWiring.Plan(SpeechWiringState.Nothing, Everywhere(null), Keys(false));

        Assert.Equal(Edge, plan.Next.Of(VoiceGroup.Aboard)?.ProviderId);
    }

    /// <summary>
    /// The phase's own requirement, and the one that could not be got wrong before there were six
    /// slots: <c>ElevenLabsTtsProvider.MaxConcurrent</c> gates the <em>account</em>, so five slots
    /// naming Edge must not produce five Edge clients each believing it owns the whole budget.
    /// </summary>
    [Fact]
    public void SlotsSharingAProviderShareOneClient()
    {
        var plan = SpeechWiring.Plan(SpeechWiringState.Nothing, Split(Eleven, Edge), Keys(true));

        Assert.Equal([Edge, Eleven], plan.Build);

        // All six moved — nothing was held — but only two clients answer for them.
        Assert.Equal(6, plan.Rewire.Count);
    }

    [Fact]
    public void OneSlotLeavingASharedProviderDisposesNothing()
    {
        var held = Holding(Everywhere(Edge));

        var moved = VoiceGroups.All.ToDictionary(
            slot => slot.Group,
            slot => slot.Group == VoiceGroup.Carrier ? Eleven : Edge);

        var plan = SpeechWiring.Plan(held, moved, Keys(true));

        // Edge is still speaking for five slots. Disposing it because one left is the failure
        // this shape exists to prevent — and it would arrive as five silent slots, not one.
        Assert.Empty(plan.Dispose);
        Assert.Equal([Eleven], plan.Build);
        Assert.Equal([VoiceGroup.Carrier], plan.Rewire);
    }

    [Fact]
    public void TheLastSlotLeavingAProviderReleasesIt()
    {
        var held = Holding(Split(Eleven, Edge));

        var plan = SpeechWiring.Plan(held, Everywhere(Edge), Keys(true));

        Assert.Equal([Eleven], plan.Dispose);
        Assert.Empty(plan.Build);
        Assert.Equal([VoiceGroup.Aboard], plan.Rewire);
    }

    [Fact]
    public void AnUnrelatedSlotMovingLeavesTheOthersAlone()
    {
        var held = Holding(Everywhere(Edge));

        var moved = VoiceGroups.All.ToDictionary(
            slot => slot.Group,
            slot => slot.Group == VoiceGroup.AnyoneInRange ? None : Edge);

        var plan = SpeechWiring.Plan(held, moved, Keys(true));

        // The slot went silent; nothing else did, and Edge is neither rebuilt nor released.
        Assert.Equal([VoiceGroup.AnyoneInRange], plan.Rewire);
        Assert.Empty(plan.Dispose);
        Assert.Empty(plan.Build);
    }

    /// <summary>
    /// The whole afternoon, in order: start on Edge, try ElevenLabs, paste the key, change an
    /// unrelated row, go back to Edge. Five applies, three of which must touch nothing.
    /// </summary>
    [Fact]
    public void AnAfternoonOfHandTestingRebuildsExactlyTwice()
    {
        var held = SpeechWiringState.Nothing;
        var built = 0;
        var refetches = 0;

        foreach (var (provider, key) in new (string, bool)[]
                 {
                     (Edge, true),      // launch
                     (Eleven, false),   // chosen, no key yet
                     (Eleven, true),    // key pasted
                     (Eleven, true),    // an unrelated row changed
                     (Edge, true),      // back again
                 })
        {
            var plan = SpeechWiring.Plan(held, Everywhere(provider), Keys(key));
            held = plan.Next;

            built += plan.Build.Count;
            refetches += plan.RefetchVoices.Count;
        }

        // Launch, and the switch to ElevenLabs, and the switch back: three clients.
        Assert.Equal(3, built);

        // Those three, plus the key arriving. Not the unrelated row.
        Assert.Equal(4, refetches);
    }
}
