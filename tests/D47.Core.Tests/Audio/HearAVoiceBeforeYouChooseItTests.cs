using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// What a voice says when it is auditioned (list.md Phase 19, "Hear a voice before you choose
/// it").
/// <para>
/// The line is the core's own, because a voice is being cast for a character: "Bill - Wise,
/// Mature, Balanced" and "George - Warm, Captivating Storyteller" are both true and neither
/// tells you which one is Warden. A generic sample answers a different question.
/// </para>
/// </summary>
public class HearAVoiceBeforeYouChooseItTests
{
    [Fact]
    public void TheLineIsTheCoresOwnOpening()
    {
        var said = AuditionLine.For(PersonaCatalog.Warden);

        Assert.StartsWith("Commander. I'm awake", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AndOnlyTheFirstSentenceOfIt()
    {
        // Warden's introduction runs to five sentences. Every press of a play glyph is a synthesis
        // request billed by the character, and the first sentence is where a core's voice is
        // most itself anyway — it is written as the opening.
        Assert.True(
            AuditionLine.For(PersonaCatalog.Warden).Length < PersonaCatalog.Warden.Intro.Length / 2,
            "the audition line is most of the whole introduction.");
    }

    [Fact]
    public void EveryCoreHasALineAndNoneOfThemIsAFragment()
    {
        Assert.All(PersonaCatalog.All, persona =>
        {
            var said = AuditionLine.For(persona);

            Assert.False(string.IsNullOrWhiteSpace(said), $"{persona.Id} auditions with nothing.");

            // A sample that stops mid-clause tells you about d47's string handling rather than
            // about the voice.
            Assert.True(
                said.EndsWith('.') || said.EndsWith('!') || said.EndsWith('?') || said.EndsWith('…'),
                $"{persona.Id} auditions with \"{said}\", which does not finish.");
        });
    }

    /// <summary>
    /// The picker states a price before anything is pressed, and the estimate behind it has to be
    /// somewhere near the truth or the figure is worse than no figure.
    /// </summary>
    [Fact]
    public void TheTypicalLengthIsInsideTheRangeTheCoresActuallyProduce()
    {
        var lengths = PersonaCatalog.All.Select(persona => AuditionLine.For(persona).Length).ToArray();

        Assert.InRange(AuditionLine.TypicalCharacters, lengths.Min(), lengths.Max());
    }

    /// <summary>
    /// Six of the eleven cores open by addressing the Commander, so the literal first sentence
    /// of Warden's introduction is the single word "Commander." — a sample too short to tell one
    /// voice from another, which is the entire point of auditioning.
    /// </summary>
    [Fact]
    public void NoCoreAuditionsWithASingleWord()
    {
        Assert.All(PersonaCatalog.All, persona =>
        {
            var said = AuditionLine.For(persona);

            Assert.True(
                said.Length >= 20,
                $"{persona.Id} auditions with \"{said}\", which is not enough to judge a voice by.");
        });
    }

    [Fact]
    public void TheNamedRolesAudititionAsThemselvesRatherThanAsTheShipsAi()
    {
        // The carrier's captain and its tower are different people from the ship's AI and have
        // no written introduction, so they say what they are actually for — a Commander casting
        // a tower voice is listening for something different from a companion.
        Assert.Contains("Carrier bridge", AuditionLine.For(VoiceRole.CarrierCaptain), StringComparison.Ordinal);
        Assert.Contains("Tower", AuditionLine.For(VoiceRole.TowerControl), StringComparison.Ordinal);
        Assert.NotEqual(
            AuditionLine.For(VoiceRole.CarrierCaptain),
            AuditionLine.For(VoiceRole.TowerControl));
    }

    [Fact]
    public void ARoleWithNothingWrittenForItStillSaysSomething()
    {
        Assert.False(string.IsNullOrWhiteSpace(AuditionLine.For(VoiceRole.Crew)));
    }
}
