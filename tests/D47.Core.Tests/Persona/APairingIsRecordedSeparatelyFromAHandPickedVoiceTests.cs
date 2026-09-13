using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// The pairing pass and a hand-picked voice write the same live slot, so the pairing's own choice
/// only survives being overwritten if it is kept somewhere a hand-pick never touches (#85).
/// </summary>
public class APairingIsRecordedSeparatelyFromAHandPickedVoiceTests
{
    private static Dictionary<string, string> Nothing() => new(StringComparer.Ordinal);

    [Fact]
    public void ACoreThatChangedIsRecorded()
    {
        var before = Nothing();
        var after = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "en-US-GuyNeural" };

        var recorded = VoicePairing.WithPairingsRecorded(Nothing(), before, after);

        Assert.Equal("en-US-GuyNeural", recorded["warden"]);
    }

    [Fact]
    public void ACoreThatDidNotChangeIsNotAddedToTheRecord()
    {
        // A repair pass leaves an untouched core's live value exactly as it found it — hand-picked or
        // not — and the record must not read that as a pairing.
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "hand-picked" };
        var after = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "hand-picked" };

        var recorded = VoicePairing.WithPairingsRecorded(Nothing(), before, after);

        Assert.DoesNotContain("warden", recorded.Keys);
    }

    [Fact]
    public void APreviouslyRecordedCoreThatDidNotChangeIsKept()
    {
        var already = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = "en-US-AriaNeural" };
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = "en-US-AriaNeural" };
        var after = before;

        var recorded = VoicePairing.WithPairingsRecorded(already, before, after);

        Assert.Equal("en-US-AriaNeural", recorded["cora"]);
    }

    [Fact]
    public void AChangedCoreOverwritesWhatWasRecordedForItBefore()
    {
        var already = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "old-pairing" };
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "old-pairing" };
        var after = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "new-pairing" };

        var recorded = VoicePairing.WithPairingsRecorded(already, before, after);

        Assert.Equal("new-pairing", recorded["warden"]);
    }
}
