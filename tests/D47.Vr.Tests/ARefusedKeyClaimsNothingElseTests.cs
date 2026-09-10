using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>A second copy of d47 finding the overlay keys taken.</summary>
public class ARefusedKeyClaimsNothingElseTests
{
    [Fact]
    public void AKeyAnotherCopyOwnsIsReportedAsOwnedRatherThanFailed()
    {
        var openVr = new FakeOpenVr();
        openVr.KeysTaken.Add("com.dseelinger.D47.panel");

        var runtime = new SteamVrRuntime(
            [new FakeSurface()],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        try
        {
            var start = runtime.Start();

            Assert.Equal(VrStartOutcome.AlreadyOwned, start.Outcome);
            Assert.NotNull(start.Detail);
            Assert.Contains("Close it", start.Detail);
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>And the quads claimed before the refusal are given back rather than left behind.</summary>
    [Fact]
    public void TheQuadsTakenBeforeTheRefusalAreGivenBack()
    {
        var openVr = new FakeOpenVr();
        openVr.KeysTaken.Add("com.dseelinger.D47.panel");

        var runtime = new SteamVrRuntime(
            [new FakeSurface { Surface = VrSurface.Captions }, new FakeSurface()],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        try
        {
            Assert.Equal(VrStartOutcome.AlreadyOwned, runtime.Start().Outcome);

            // Captions went through, the panel did not, and nothing was tried after it.
            Assert.Equal(["com.dseelinger.D47.captions"], openVr.Created);
            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.CreateOverlay)));
            Assert.Empty(openVr.Live);
        }
        finally
        {
            runtime.Stop();
        }
    }
}
