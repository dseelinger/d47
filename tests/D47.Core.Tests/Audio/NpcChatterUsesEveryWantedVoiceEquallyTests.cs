using Xunit;
using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

public sealed class NpcChatterUsesEveryWantedVoiceEquallyTests
{
    [Fact]
    public void AVoiceAfterALongRunOfTheOtherGenderIsNotDrawnMoreOften()
    {
        var pool = new[] { "f1", "f2", "f3", "m1", "m2", "m3", "m4", "m5", "m6", "f4" };
        var cast = new VoiceCast
        {
            Pool = pool,
            Feminine = new HashSet<string>(["f1", "f2", "f3", "f4"], StringComparer.OrdinalIgnoreCase),
        };

        var counts = pool.Where(v => v.StartsWith('f')).ToDictionary(v => v, _ => 0);

        for (uint seed = 1; seed <= 4000; seed += 2)
        {
            counts[cast.Roster(1, seed * 2654435761u | 1u)[0]]++;
        }

        Assert.All(counts.Values, count => Assert.True(count > 0));
        Assert.True(counts.Values.Max() <= 2 * counts.Values.Min(), string.Join(", ", counts));
    }

    [Fact]
    public void ASenderDrawsEvenlyAmongTheVoicesLeft()
    {
        var pool = new[] { "a", "b", "c", "d", "e", "f" };
        var counts = pool.ToDictionary(v => v, _ => 0);

        for (var i = 0; i < 3000; i++)
        {
            var cast = new VoiceCast { Pool = pool };
            counts[cast.ForSender("Npc " + i, false, VoiceRole.Comms).VoiceId!]++;
        }

        Assert.True(counts.Values.Max() <= 2 * counts.Values.Min(), string.Join(", ", counts));
    }
}
