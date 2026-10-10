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

    /// <summary>SHA-256 of what each effect alone produces, and of eight of them together.</summary>
    [Theory]
    [InlineData("stutter", "1689245D18D2E3334F20871A143ACD8C8125606DB0AC249287C6CE700521A8F0")]
    [InlineData("monotone", "415E092AD5D24CFBBFDEC75FAD310673B3F5AA8643005103C806D6877CEA9A5F")]
    [InlineData("steppedPitch", "6F2EA8470F63134DD2A8081F3736EFA6271FE93C2BF263725351CF323AB468C6")]
    [InlineData("cylon", "E155F6EE4101EC92D6041A68599B7D17744C9A94ACDDAAFE7A1AD0D4E6FC197E")]
    [InlineData("whisper", "A7AF84BED1ECE74F3024688DB02D4BB3E62E803B3613C0DC516E10158AD7440C")]
    [InlineData("pitchDown", "126C22285CC8CBD71E0BDDC5E4518D4754F57BA7AE169BAACFD13EB3C667ECC3")]
    [InlineData("octaveDown", "C0CD23DCBADA4B55794603D36989530C199C0D17F908433225DE4D4FFAC58775")]
    [InlineData("chorus", "10AACCCE393E58CED184ABC1AC2E15349CE5628737A62594243A0F3A301C5EE2")]
    [InlineData("hive", "0632A6F95FE4988CA7FE9ED6A04DFEF71305EE42AFE7F7EA65BCF3275516EBCE")]
    [InlineData("flanger", "1A4C944FBA5A14A7810BFDE817C13EC67278A9707CC84276ADB7ACCBE53B4EA1")]
    [InlineData("phaser", "EB1DB972C7A250ADE4913CBC2D466C4B6F2696CBB3E61359073C0498F38B3367")]
    [InlineData("wah", "6BC1A2C6CC7FE75FC8CCC1412CC310A361E2EE07F1A3199DB47B11F1413BBC32")]
    [InlineData("comb", "99167B57B9E57E3537212073D6D8F5C68C187FDF7D35E51C1F638D2402F3BF4A")]
    [InlineData("ringMod", "3647C6EF65CD460B0B0D178B145514B7C4B57B05803F59BFD0623FA2DD90CD02")]
    [InlineData("deepRingMod", "F3EF504C6A78727F475E8E7B479C5959C4A26858185622069D0611BAA0C64187")]
    [InlineData("tremolo", "E0C0B2D9A8949AE0507A8F4DFB6A66AE4118D76FA8CEAE166FD7E0387F4F6F8F")]
    [InlineData("overdrive", "CAB3D39B3CB990F1A3740EEA95DD41C37AEEDA86F8D64473B353AC0429591659")]
    [InlineData("bitcrusher", "45925E2EA26EF9BDC2203FB405D352A26734D4EF39F536811CBD730137BD065F")]
    [InlineData("glitch", "A5EBBDA3C7A17824D9EDC95E2B76BC2F98846D3BA65BB0AD0A91946EE8A20C5B")]
    [InlineData("helmet", "33F1AE770250A653AA4E3562EA56663879B1FAF2C8133F5321ABAC4C152D0ADF")]
    [InlineData("hologram", "826AAD2E5BA39C067837FB5BFFA6EA2A73D978334B86EB96B8EC5FAD5EEB6F39")]
    [InlineData("reverseReverb", "9B8C984305E0910A81EE67E59CEBBFE61FD24EA1A38D331B26A453A6FAC544B9")]
    [InlineData("shimmer", "715F5E442A6F659F0850C3D5EF38D87798DE6C160DCA2AC4CCC9A258502B326F")]
    [InlineData("reverb", "03DFC3DDAEA3FD4D9645CA2C9C01CBB221858E470DAC2B62D0972668FBF05DEC")]
    [InlineData("respirator", "24337CB4891762F1969392506FD3E2E53E540A73BA272A329D9070C62622C436")]
    [InlineData(
        "cylon,pitchDown,octaveDown,chorus,comb,ringMod,glitch,reverb",
        "AE771BDABE98F689A49F178E7D173E2EC2AE32B6BB9564C4CD90A89CDA964ABF")]
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

            if (!GuardianVoiceTests.Lengthening.Contains(id))
            {
                Assert.Equal(line.Pcm.Length, treated.Pcm.Length);
            }

            var wet = GuardianVoiceTests.Samples(treated).AsSpan(0, dry.Length);
            var decibels = 20 * Math.Log10(GuardianVoiceTests.Rms(wet) / GuardianVoiceTests.Rms(dry));

            Assert.True(Math.Abs(decibels) < 1, $"{id} at level {level} moved the level {decibels:F2} dB");
        }
    }
}
