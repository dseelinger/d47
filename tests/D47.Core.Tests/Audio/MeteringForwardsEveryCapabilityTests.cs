using System.Reflection;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// <see cref="MeteredTtsProvider"/> answers for the provider it wraps, on every question
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>The failure this catches is silent by construction.</b> Every member of
/// <see cref="ITtsProvider"/> that describes a capability has a default implementation, because
/// most providers do not have the capability — so a member the decorator does not restate is not a
/// compiler error. It is a wrapper that answers "no" on behalf of a provider that would have said
/// yes, and since every caller reaches the provider through the wrapper, that is the only answer
/// anything ever sees.
/// </para>
/// <para>
/// <b>It has happened twice.</b> <c>Phonemes</c> was added for the audio recorder (#164) and never
/// forwarded, so the phoneme column has been empty for the local voice ever since.
/// <c>ReadsAudioTags</c> and <c>GroupsSentencesUpTo</c> arrived with #291 and went the same way —
/// caught only by driving the built app and finding v3 selected, speaking, and doing neither of the
/// two things it had been selected for.
/// </para>
/// </summary>
public class MeteringForwardsEveryCapabilityTests
{
    /// <summary>
    /// Returned as <see cref="ITtsProvider"/>, which is how every caller holds it. Asking the
    /// concrete type would turn a missing forward into a compile error here and hide the thing
    /// being tested — the app sees only the interface, and the interface is where a missing
    /// forward silently answers "no".
    /// </summary>
    private static ITtsProvider Wrapping(ITtsProvider inner) => new MeteredTtsProvider(inner, new SpeechSpend());

    /// <summary>
    /// The capabilities themselves, stated rather than reflected, so this reads as a list of what
    /// a wrapped provider must still be able to say.
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
    /// And an incapable one is still incapable — a decorator that hardcoded "yes" would pass the
    /// test above and be worse than the bug it fixed.
    /// </summary>
    [Fact]
    public void AndAnIncapableOneIsNotGivenCapabilitiesItLacks()
    {
        var metered = Wrapping(new FakeTtsProvider());

        Assert.False(metered.ReadsAudioTags);
        Assert.Equal(0, metered.GroupsSentencesUpTo);
    }

    /// <summary>
    /// <b>The gate proper: no member of the interface may be left to its default here.</b>
    /// <para>
    /// Reflection rather than a hand-written list, because a hand-written list is exactly what was
    /// missing both times. A member declared on <see cref="ITtsProvider"/> and not declared on
    /// <see cref="MeteredTtsProvider"/> is one this class inherits the default of, which is the
    /// shape of both bugs — so the assertion is simply that the decorator restates all of them.
    /// </para>
    /// </summary>
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
