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
    [InlineData("cylon", "73AE78F003A0DD26DD0F0AD067CAA8F957A7487B5D105287E4AF2E9060761574")]
    [InlineData("whisper", "A7AF84BED1ECE74F3024688DB02D4BB3E62E803B3613C0DC516E10158AD7440C")]
    [InlineData("pitchDown", "27298A461112709A77633C8823CD9A3AB4CDC99BCE98BE40B50BD3EEB71C254C")]
    [InlineData("octaveDown", "8CD1D46E4F634A2635443C2398BE56EEE10A5751F019414A9F5FEA8DB790D658")]
    [InlineData("chorus", "65B8A8B9ED362E06D66A0ECDEBC3983A5C091540D34D903B661B9E359355AD3F")]
    [InlineData("hive", "B0DC0C92AAAA061C1726F4E0572CD5ACDDE84FAB45E4A1142DF96262D03CDBC9")]
    [InlineData("flanger", "F195F9EEEA207C6D2ED4367050192D682D04C4877A5F5D5A3AE06D0DFE14780D")]
    [InlineData("phaser", "8731E5848F0AC28FC9C6568CB282EB46AE100C101ACCCA8669DB6EE3F8A0BC38")]
    [InlineData("wah", "DAE7AE1699027EEDAC5398229364F2DB6E6B65E9CB00078CA4DC13E829A4612C")]
    [InlineData("comb", "ECB213432FA57F28260AEA69FF2E6F02F47A5EADFCF64329898098DFA5EB7048")]
    [InlineData("ringMod", "4161E2EE35D344B4B6498FB298D7BD3034B9A109CD6198F8D37086A39246F81D")]
    [InlineData("deepRingMod", "09F40DD1847F72EB21AAD6A398AF245F95AF5A368D011B709F124C3AEAB00934")]
    [InlineData("tremolo", "68F0463B758405716C1506D316C5727E233D445CA7497035DC5F52D6F3DFC550")]
    [InlineData("overdrive", "592A4486FAB7801EF45FED2B060A2962D732F62D2AA5E1F80B19AD6F1B7BC740")]
    [InlineData("bitcrusher", "45925E2EA26EF9BDC2203FB405D352A26734D4EF39F536811CBD730137BD065F")]
    [InlineData("glitch", "E35D8F6FFDE0057C7F283B8789666C07299FF35D45E243E8EC12FB8D12BCB540")]
    [InlineData("helmet", "14C6ADBEC30D0389460BA8B4D60B132CE3DFA38FAB3AF585A8EF49874900E0C9")]
    [InlineData("hologram", "FE56A148D475E58871A2241ED5E825C3DA4708FF902385B2FD1A0CC43C473F94")]
    [InlineData("reverseReverb", "9B8C984305E0910A81EE67E59CEBBFE61FD24EA1A38D331B26A453A6FAC544B9")]
    [InlineData("shimmer", "B514A7A1127C62F5B64447073B2918CBAE4DA47DC50F3C5FC8EA9E84344D1129")]
    [InlineData("reverb", "93094FE9AC47799356D8DC3EFB342F854E03C50452F7757489C844267A37C1E6")]
    [InlineData("respirator", "24337CB4891762F1969392506FD3E2E53E540A73BA272A329D9070C62622C436")]
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
