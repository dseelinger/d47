using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Naming a voice that a slot other than the ship's is speaking in
/// (<a href="https://github.com/dseelinger/d47/issues/149">#149</a>).
/// <para>
/// <c>TheLogSaysWhichVoiceSpokeTests</c> proves the line carries a name once one is attached, and
/// that is a different claim from a name being attached: the resolver only ever read the ship's
/// own list, so on a Commander running three providers at once a re-voiced NPC arrived as
/// <em>Spoken by Don Tazeme in FwuKjlVpi0N3exead7ji</em> — an opaque id, with nothing to say
/// whose voice it was. The name had been fetched; it was filed under the slot that fetched it.
/// </para>
/// </summary>
public class APoolVoiceIsNamedTooTests
{
    /// <summary>
    /// Doug's own configuration on the day this was reported: the companion on the local voice,
    /// the carrier and the NPCs on ElevenLabs, everyone else on Edge.
    /// </summary>
    private static VoiceCatalogue Slot(VoiceGroup group) => group switch
    {
        VoiceGroup.Aboard => VoiceCatalogue.Of([new VoiceInfo("am_michael", "Michael", "en-US", "male")]),

        VoiceGroup.Carrier or VoiceGroup.Npcs => VoiceCatalogue.Of(
            [new VoiceInfo("FwuKjlVpi0N3exead7ji", "Boe Dock", "american", "male")]),

        _ => VoiceCatalogue.Of([new VoiceInfo("en-GB-RyanNeural", "Ryan", "en-GB", "Male")]),
    };

    /// <summary>
    /// The reported case. A voice <see cref="VoicePool"/> handed to a sender out of the NPC
    /// slot's provider, named as a cast one is.
    /// </summary>
    [Fact]
    public void AVoiceFromAnotherSlotsProviderIsNamed()
    {
        Assert.Equal("Boe Dock", VoiceGroups.NameFor(Slot, "FwuKjlVpi0N3exead7ji"));
    }

    /// <summary>
    /// And the ship's own is still resolved out of the ship's own list, which is the answer this
    /// gave before. Widening the search may not change a line that was already right.
    /// </summary>
    [Fact]
    public void TheShipsOwnVoiceIsUnchanged()
    {
        Assert.Equal("Michael", VoiceGroups.NameFor(Slot, "am_michael"));
    }

    /// <summary>
    /// Every slot, not merely the two that happen to be first. The five over-the-air ones are
    /// exactly where other people's words are spoken, and where an unnamed id is least useful.
    /// </summary>
    [Fact]
    public void EverySlotsListIsSearched()
    {
        Assert.Equal("Ryan", VoiceGroups.NameFor(Slot, "en-GB-RyanNeural"));
    }

    /// <summary>
    /// Ids are matched the way they are everywhere else in d47 — Edge and ElevenLabs disagree
    /// about capitalisation in every other field, and a settings file may hold either.
    /// </summary>
    [Fact]
    public void TheMatchIgnoresCase()
    {
        Assert.Equal("Boe Dock", VoiceGroups.NameFor(Slot, "fwukjlvpi0n3exead7ji"));
    }

    /// <summary>
    /// Nothing to resolve is a normal state and not a fault: an id typed by hand, or any id at
    /// all before a provider's list has arrived. Null, so the caller decides what to show — which
    /// for the log line is the id on its own, exactly as it always was.
    /// </summary>
    [Theory]
    [InlineData("something-hand-typed")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUnknownVoiceResolvesToNothing(string? id)
    {
        Assert.Null(VoiceGroups.NameFor(Slot, id));
    }

    /// <summary>
    /// A slot whose provider has not answered yet is a silent catalogue rather than an error, and
    /// the search simply carries on to the next one.
    /// </summary>
    [Fact]
    public void ASlotThatHasNotAnsweredIsSteppedPast()
    {
        Assert.Equal(
            "Boe Dock",
            VoiceGroups.NameFor(
                group => group == VoiceGroup.Npcs ? Slot(group) : VoiceCatalogue.Silent,
                "FwuKjlVpi0N3exead7ji"));
    }
}
