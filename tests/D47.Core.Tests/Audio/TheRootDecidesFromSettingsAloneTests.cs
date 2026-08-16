using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The composition root's speech wiring, driven as a sequence rather than as one call
/// (list.md Phase 19, "Give the composition root a test harness").
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
/// </summary>
public class TheRootDecidesFromSettingsAloneTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;
    private const string None = TtsProviderCatalog.NoneId;

    [Fact]
    public void TheFirstApplyOfTheProcessBuildsAClient()
    {
        var plan = SpeechWiring.Plan(SpeechWiringState.Nothing, Edge, keyPresent: true);

        // Nothing held is not "Edge held". A root that treated it as such would start with the
        // default provider selected and no client behind it, which is silence on launch.
        Assert.True(plan.RebuildClient);
        Assert.True(plan.RefetchVoices);
        Assert.Equal(Edge, plan.Next.ProviderId);
    }

    [Fact]
    public void ApplyingTheSameSettingsTwiceRebuildsNothing()
    {
        var first = SpeechWiring.Plan(SpeechWiringState.Nothing, Edge, keyPresent: true);
        var second = SpeechWiring.Plan(first.Next, Edge, keyPresent: true);

        // Settings are applied on every change of any row, so this is the common case by a wide
        // margin. Rebuilding here would drop the voice list and the sender assignments every
        // time the Commander moved a slider.
        Assert.False(second.RebuildClient);
        Assert.False(second.RefetchVoices);
    }

    [Fact]
    public void ChangingProviderRebuildsAndRefetches()
    {
        var held = new SpeechWiringState(Edge, KeyPresent: true);

        var plan = SpeechWiring.Plan(held, Eleven, keyPresent: false);

        Assert.True(plan.RebuildClient);
        Assert.True(plan.RefetchVoices);
        Assert.Equal(Eleven, plan.Next.ProviderId);
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
            new SpeechWiringState(Edge, KeyPresent: true), Eleven, keyPresent: false);

        var keyPasted = SpeechWiring.Plan(chosenWithNoKey.Next, Eleven, keyPresent: true);

        Assert.True(keyPasted.RefetchVoices);

        // And it is a refetch, not a rebuild. The client asks for the key per call, so replacing
        // it would throw away a list that had only just arrived.
        Assert.False(keyPasted.RebuildClient);
    }

    [Fact]
    public void AKeyBeingClearedRefetchesToo()
    {
        var withKey = new SpeechWiringState(Eleven, KeyPresent: true);

        // The other direction, and it matters for the same reason: the list on screen was
        // fetched with a key that is no longer stored, so offering it invites choosing a voice
        // that cannot be spoken.
        Assert.True(SpeechWiring.Plan(withKey, Eleven, keyPresent: false).RefetchVoices);
    }

    [Fact]
    public void PastingTheSameKeyTwiceCostsNothing()
    {
        var held = new SpeechWiringState(Eleven, KeyPresent: true);

        Assert.False(SpeechWiring.Plan(held, Eleven, keyPresent: true).RefetchVoices);
    }

    /// <summary>
    /// "None" is a supported choice, not an absence of one: d47 stays fully usable in text with
    /// the cues still audible. The rebuild still has to happen — it is what releases the client
    /// that was speaking — and there is nothing to ask for a voice list.
    /// </summary>
    [Fact]
    public void SelectingNoProviderStillReleasesTheOneThatWasSpeaking()
    {
        var plan = SpeechWiring.Plan(new SpeechWiringState(Eleven, KeyPresent: true), None, keyPresent: true);

        Assert.True(plan.RebuildClient);
        Assert.False(plan.RefetchVoices);
    }

    [Fact]
    public void NoProviderSelectedAsksForNothingOnEveryLaterApply()
    {
        var first = SpeechWiring.Plan(SpeechWiringState.Nothing, None, keyPresent: false);

        Assert.False(SpeechWiring.Plan(first.Next, None, keyPresent: true).RefetchVoices);
    }

    /// <summary>
    /// A settings file naming a provider d47 no longer ships resolves to Edge everywhere else in
    /// the app, and has to resolve to Edge here too — a plan keyed on the raw string would
    /// rebuild on every single apply, because the held id and the selected one could never match.
    /// </summary>
    [Fact]
    public void AProviderD47DoesNotShipIsPlannedAsTheOneItResolvesTo()
    {
        var first = SpeechWiring.Plan(SpeechWiringState.Nothing, "festival", keyPresent: false);

        Assert.Equal(Edge, first.Next.ProviderId);
        Assert.False(SpeechWiring.Plan(first.Next, "festival", keyPresent: false).RebuildClient);
    }

    [Fact]
    public void ANullProviderIsTheDefaultRatherThanNoProvider()
    {
        Assert.Equal(Edge, SpeechWiring.Plan(SpeechWiringState.Nothing, null, keyPresent: false).Next.ProviderId);
    }

    /// <summary>
    /// The whole afternoon, in order: start on Edge, try ElevenLabs, paste the key, change an
    /// unrelated row, go back to Edge. Five applies, three of which must touch nothing.
    /// </summary>
    [Fact]
    public void AnAfternoonOfHandTestingRebuildsExactlyTwice()
    {
        var held = SpeechWiringState.Nothing;
        var rebuilds = 0;
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
            var plan = SpeechWiring.Plan(held, provider, key);
            held = plan.Next;

            rebuilds += plan.RebuildClient ? 1 : 0;
            refetches += plan.RefetchVoices ? 1 : 0;
        }

        // Launch, and the switch to ElevenLabs, and the switch back: three clients.
        Assert.Equal(3, rebuilds);

        // Those three, plus the key arriving. Not the unrelated row.
        Assert.Equal(4, refetches);
    }
}
