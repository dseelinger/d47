using System.Security.Cryptography;
using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#478: the ticked Guardian voice effects run in the order set, each at its level.</summary>
public class GuardianEffectsRunInTheOrderAndAtTheLevelSetTests
{
    private const double BasePitch = 200;

    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    public static TheoryData<string> EachEffect => [.. GuardianVoice.Table.Select(effect => effect.Id)];

    private static IReadOnlyList<GuardianVoiceEffect> At(string id, int level) =>
        [.. GuardianVoice.Defaults.Select(effect => effect.Id == id ? effect with { Ticked = true, Level = level } : effect)];

    private static byte[] Treated(IReadOnlyList<GuardianVoiceEffect> effects) =>
        GuardianVoice.Apply(Line, effects, BasePitch).Pcm.ToArray();

    /// <summary>SHA-256 of what each effect alone, and all eight together, produced before levels and order existed.</summary>
    [Theory]
    [InlineData("cylon", "73AE78F003A0DD26DD0F0AD067CAA8F957A7487B5D105287E4AF2E9060761574")]
    [InlineData("pitchDown", "27298A461112709A77633C8823CD9A3AB4CDC99BCE98BE40B50BD3EEB71C254C")]
    [InlineData("octaveDown", "8CD1D46E4F634A2635443C2398BE56EEE10A5751F019414A9F5FEA8DB790D658")]
    [InlineData("chorus", "65B8A8B9ED362E06D66A0ECDEBC3983A5C091540D34D903B661B9E359355AD3F")]
    [InlineData("comb", "ECB213432FA57F28260AEA69FF2E6F02F47A5EADFCF64329898098DFA5EB7048")]
    [InlineData("ringMod", "4161E2EE35D344B4B6498FB298D7BD3034B9A109CD6198F8D37086A39246F81D")]
    [InlineData("glitch", "E35D8F6FFDE0057C7F283B8789666C07299FF35D45E243E8EC12FB8D12BCB540")]
    [InlineData("reverb", "93094FE9AC47799356D8DC3EFB342F854E03C50452F7757489C844267A37C1E6")]
    [InlineData(
        "cylon,pitchDown,octaveDown,chorus,comb,ringMod,glitch,reverb",
        "32236C76B2CA621872AA5E97600A290D7DC04DBA0CA62629C7793AADF868C53B")]
    public void AtDefaultLevelsInTheDefaultOrderTheBytesAreUnchanged(string ids, string sha256)
    {
        var treated = Treated(GuardianVoiceTests.Ticking(ids.Split(',')));

        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(treated)));
    }

    [Fact]
    public void SwappingTwoTickedEffectsChangesTheOutput()
    {
        var inOrder = GuardianVoiceTests.Ticking("comb", "ringMod");
        var comb = inOrder.Single(effect => effect.Id == "comb");
        var ringMod = inOrder.Single(effect => effect.Id == "ringMod");
        var swapped = inOrder.Select(effect => effect.Id == "comb" ? ringMod : effect.Id == "ringMod" ? comb : effect).ToList();

        Assert.NotEqual(Treated(inOrder), Treated(swapped));
    }

    [Theory]
    [MemberData(nameof(EachEffect))]
    public void ChangingATickedEffectsLevelChangesTheOutput(string id)
    {
        // Stepped pitch's hold changes nothing on a line of one pitch; a glide checks it instead.
        var line = id == "steppedPitch" ? MonotoneAndSteppedPitchMoveOnlyThePitchTests.Glide() : Line;
        var standard = GuardianVoice.Find(id)!.DefaultLevel;
        var atDefault = GuardianVoice.Apply(line, At(id, standard), BasePitch).Pcm.ToArray();

        foreach (var level in new[] { GuardianVoice.LowestLevel, GuardianVoice.HighestLevel }.Where(l => l != standard))
        {
            Assert.NotEqual(atDefault, GuardianVoice.Apply(line, At(id, level), BasePitch).Pcm.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(EachEffect))]
    public void AnUntickedEffectsLevelDoesNotChangeTheOutput(string id)
    {
        var other = id == "comb" ? "ringMod" : "comb";
        var baseline = Treated(GuardianVoiceTests.Ticking(other));

        foreach (var level in new[] { GuardianVoice.LowestLevel, GuardianVoice.HighestLevel })
        {
            var effects = GuardianVoiceTests.Ticking(other)
                .Select(effect => effect.Id == id ? effect with { Level = level } : effect)
                .ToList();

            Assert.Equal(baseline, Treated(effects));
        }
    }

    [Theory]
    [MemberData(nameof(EachEffect))]
    public void AtLevelOneAndLevelTwentyTheLengthAndLoudnessHold(string id)
    {
        var line = GuardianVoiceTests.Voiced(150, seconds: 3.0);
        var dry = GuardianVoiceTests.Samples(line);

        foreach (var level in new[] { GuardianVoice.LowestLevel, GuardianVoice.HighestLevel })
        {
            var treated = GuardianVoice.Apply(line, At(id, level), BasePitch);

            if (id != "reverb" && id != "stutter")
            {
                Assert.Equal(line.Pcm.Length, treated.Pcm.Length);
            }

            var wet = GuardianVoiceTests.Samples(treated).AsSpan(0, dry.Length);
            var decibels = 20 * Math.Log10(GuardianVoiceTests.Rms(wet) / GuardianVoiceTests.Rms(dry));

            Assert.True(Math.Abs(decibels) < 1, $"{id} at level {level} moved the level {decibels:F2} dB");
        }
    }
}
