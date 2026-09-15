using System.Numerics;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The cursor sprite is resubmitted only when the kind actually changes, since a serve runs ten times a
/// second (#191).
/// </summary>
public class TheCursorArrowResubmitsOnlyOnChangeTests
{
    private static readonly VrPose Head = new(Vector3.Zero, Quaternion.Identity);

    [Fact]
    public void TheSameKindIsSubmittedOnceAcrossManyCalls()
    {
        var openVr = new FakeOpenVr();
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr) { Pointing = true };

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var cursor = openVr.Live.Single(kv => kv.Value == "com.dseelinger.D47.cursor").Key;

            // Built once, on the way up.
            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), cursor));

            for (var i = 0; i < 5; i++)
            {
                runtime.ShowCursor(new Vector3(0f, 0f, -1f), Head, VrCursor.Horizontal);
            }

            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), cursor));

            runtime.ShowCursor(new Vector3(0f, 0f, -1f), Head, VrCursor.DiagonalUp);

            Assert.Equal(3, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), cursor));

            runtime.ShowCursor(new Vector3(0f, 0f, -1f), Head, VrCursor.Ring);

            Assert.Equal(4, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), cursor));
        }
        finally
        {
            runtime.Stop();
        }
    }
}
