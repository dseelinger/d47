using System.Collections.Immutable;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>An NPC killed in this system is not offered back to a chatter exchange (#584).</summary>
public class DeadPilotsNeverSpeakInChatterTests
{
    private static readonly VoiceInfo[] Listed =
    [
        new("gb-woman", "Sonia", "en-GB", "Female"),
        new("us-man", "Guy", "en-US", "Male"),
        new("ie-woman", "Emily", "en-IE", "Female"),
        new("au-man", "William", "en-AU", "Male"),
        new("gb-man", "Ryan", "en-GB", "Male"),
        new("us-woman", "Jenny", "en-US", "Female"),
        new("plain", "Plain", "en"),
    ];

    private static VoiceCast MetFour()
    {
        var cast = new VoiceCast
        {
            Pool = VoicePool.From(Listed),
            Feminine = VoicePool.Feminine(Listed),
            British = VoicePool.British(Listed),
            Voices = Listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
            DefaultVoice = "core",
        };

        cast.Keep("Paul Curnow", "au-man");
        cast.Keep("Sillanp", "gb-man");
        cast.Keep("Jen Okafor", "us-woman");
        cast.Keep("Tam Reyes", "plain");

        return cast;
    }

    [Fact]
    public void AKilledPilotIsNotCastAndTheNextLivingOneTakesTheirPlace()
    {
        var cast = MetFour();
        var dead = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "paul curnow");

        var roster = NpcChatterRoster.Cast(cast, NpcChatterKind.Passersby, 4, "Mot", dead: dead);
        var met = roster.Slots.Where(slot => slot.Name is not null).Select(slot => slot.Name);

        Assert.Equal(["Sillanp", "Jen Okafor", "Tam Reyes"], met);
    }

    [Fact]
    public void ADeadNameMatchesWhateverItsCase()
    {
        var dead = new HashSet<string>(StringComparer.Ordinal) { "PAUL CURNOW" };

        var roster = NpcChatterRoster.Cast(MetFour(), NpcChatterKind.Hail, 4, "Mot", dead: dead);

        Assert.DoesNotContain(roster.Slots, slot => slot.Name == "Paul Curnow");
    }

    [Fact]
    public void WithNoFightTheCastIsUnchanged()
    {
        var without = NpcChatterRoster.Cast(MetFour(), NpcChatterKind.Passersby, 4, "Mot");
        var noneDead = NpcChatterRoster.Cast(MetFour(), NpcChatterKind.Passersby, 4, "Mot", dead: FightSnapshot.None.Dead);

        Assert.Equal(without.Slots, noneDead.Slots);
        Assert.Contains(without.Slots, slot => slot.Name == "Paul Curnow");
    }
}
