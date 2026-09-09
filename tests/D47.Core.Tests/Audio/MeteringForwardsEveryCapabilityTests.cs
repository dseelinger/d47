using System.Reflection;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// <see cref="MeteredTtsProvider"/> answers for the provider it wraps, on every question.
/// </summary>
public class MeteringForwardsEveryCapabilityTests
{
    /// <summary>Returned as <see cref="ITtsProvider"/>, which is how every caller holds it.</summary>
    private static ITtsProvider Wrapping(ITtsProvider inner) => new MeteredTtsProvider(inner, new SpeechSpend());

    /// <summary>
    /// The capabilities themselves, stated rather than reflected, so this reads as a list of what a
    /// wrapped provider must still be able to say.
    /// </summary>
    [Fact]
    public void ACapableProviderIsStillCapableThroughTheMeter()
    {
        var inner = new FakeTtsProvider { ReadsAudioTags = true, GroupsSentencesUpTo = 300 };
        var metered = Wrapping(inner);

        Assert.True(metered.ReadsAudioTags);
        Assert.Equal(300, metered.GroupsSentencesUpTo);
        Assert.Equal(inner.Id, metered.Id);
        Assert.Equal(inner.Name, metered.Name);
    }

    /// <summary>
    /// And an incapable one is still incapable — a decorator that hardcoded "yes" would pass the test
    /// above and be worse than the bug it fixed.
    /// </summary>
    [Fact]
    public void AndAnIncapableOneIsNotGivenCapabilitiesItLacks()
    {
        var metered = Wrapping(new FakeTtsProvider());

        Assert.False(metered.ReadsAudioTags);
        Assert.Equal(0, metered.GroupsSentencesUpTo);
    }

    /// <summary>The gate proper: no member of the interface may be left to its default here.</summary>
    [Fact]
    public void EveryMemberOfTheInterfaceIsRestatedRatherThanInherited()
    {
        var declared = typeof(MeteredTtsProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = typeof(ITtsProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(member => member.Name)

            // A property arrives as get_X alongside X; the property name is the one worth naming.
            .Where(name => !name.StartsWith("get_", StringComparison.Ordinal))
            .Where(name => !declared.Contains(name))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
             MeteredTtsProvider does not forward: {string.Join(", ", missing)}.

             Every caller reaches a provider through this decorator, so a member left to its
             interface default answers for the real provider and always answers "no". That is not
             a compiler error and it is not visible in a unit test that uses a provider directly —
             it shows up as a capability that was selected, is running, and does nothing.

             Forward it in MeteredTtsProvider, beside Billable and Phonemes.
             """);
    }
}
