using D47.Core.Speech;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class TheLocalVoiceBuildCanBeChosenTests
{

    [Fact]
    public void EveryBuildOfferedHasAMeasuredSpeed()
    {
        Assert.Equal(8, KokoroAssets.Builds.Count);

        foreach (var build in KokoroAssets.Builds)
        {
            Assert.True(
                build.RealtimeMultiple > 0,
                $"{build.Id} is offered with no measured speed behind it.");
        }
    }

    [Fact]
    public void EveryBuildHasItsOwnPinnedHash()
    {
        foreach (var build in KokoroAssets.Builds)
        {
            Assert.Equal(64, build.Asset.Sha256.Length);
            Assert.True(build.Asset.Bytes > 0, $"{build.Id} has no pinned size.");
        }

        Assert.Equal(
            KokoroAssets.Builds.Count,
            KokoroAssets.Builds.Select(build => build.Asset.Sha256).Distinct().Count());
    }

    [Fact]
    public void NoTwoBuildsAreTheSameSize() =>
        Assert.Equal(
            KokoroAssets.Builds.Count,
            KokoroAssets.Builds.Select(build => build.Asset.Bytes).Distinct().Count());

    [Fact]
    public void EveryLabelSaysBothSizeAndTheWait()
    {
        foreach (var build in KokoroAssets.Builds)
        {
            Assert.Contains("MB", build.Label, StringComparison.Ordinal);
            Assert.Contains("s before it speaks", build.Label, StringComparison.Ordinal);
            Assert.StartsWith(build.Id, build.Label, StringComparison.Ordinal);

            // The unit that meant nothing, asserted absent so a later tidy-up cannot put it back.
            Assert.DoesNotContain("realtime", build.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("×", build.Label, StringComparison.Ordinal);
        }
    }

    /// <summary>The measured ratio said in seconds waited: 1.3 against 4.2 is what makes the row worth reading.</summary>
    [Fact]
    public void TheWaitIsTheBenchmarkLineDividedByTheMeasuredSpeed()
    {
        foreach (var build in KokoroAssets.Builds)
        {
            Assert.Equal(
                KokoroAssets.ReferenceLineSeconds / build.RealtimeMultiple,
                build.SecondsBeforeSpeaking,
                6);
        }

        var fastest = KokoroAssets.Builds.MinBy(build => build.SecondsBeforeSpeaking)!;
        var slowest = KokoroAssets.Builds.MaxBy(build => build.SecondsBeforeSpeaking)!;

        Assert.Equal("uint8", fastest.Id);
        Assert.Equal("quantized", slowest.Id);

        Assert.Contains("1.3 s", fastest.Label, StringComparison.Ordinal);
        Assert.Contains("4.2 s", slowest.Label, StringComparison.Ordinal);

        // Faster is a smaller number: ordering by wait is the reverse of ordering by ratio.
        Assert.Equal(
            KokoroAssets.Builds.OrderByDescending(build => build.RealtimeMultiple).Select(build => build.Id),
            KokoroAssets.Builds.OrderBy(build => build.SecondsBeforeSpeaking).Select(build => build.Id));
    }

    [Fact]
    public void SizeStillDoesNotPredictSpeed()
    {
        var smallest = KokoroAssets.Builds.MinBy(build => build.Asset.Bytes)!;
        var largest = KokoroAssets.Builds.MaxBy(build => build.Asset.Bytes)!;
        var fastest = KokoroAssets.Builds.MaxBy(build => build.RealtimeMultiple)!;

        Assert.True(smallest.RealtimeMultiple < largest.RealtimeMultiple);

        // And the fastest is neither of them: uint8, at about half fp32's size.
        Assert.NotEqual(smallest.Id, fastest.Id);
        Assert.NotEqual(largest.Id, fastest.Id);
    }

    // ---- The default, unchanged ---------------------------------------------------------------

    /// <summary>
    /// fp32 remains the default. #139 adds a choice; it does not reopen the one already made — so a
    /// Commander who never opens this row hears exactly what 0.84.0 gave them.
    /// </summary>
    [Fact]
    public void TheDefaultIsStillFp32()
    {
        Assert.Equal("fp32", KokoroAssets.DefaultBuildId);
        Assert.Equal("fp32", KokoroAssets.Builds[0].Id);
        Assert.Equal(KokoroAssets.Builds[0].Asset, KokoroAssets.Model);
    }

    /// <summary>
    /// A name this version does not publish resolves to the default rather than failing, which is what
    /// every other stored id in this repository does — and is what stops a settings file written by a
    /// later build from silencing an earlier one.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("model_int2")]
    public void AnUnknownBuildResolvesToTheDefault(string? id) =>
        Assert.Equal("fp32", KokoroAssets.BuildFor(id).Id);

    [Theory]
    [InlineData("uint8")]
    [InlineData("UINT8")]
    [InlineData("Q4f16")]
    public void ABuildIsFoundWhateverItsCase(string id) =>
        Assert.Equal(id.ToLowerInvariant(), KokoroAssets.BuildFor(id).Id);

    // ---- No orphan on disk --------------------------------------------------------------------

    /// <summary>
    /// Every build lands as <c>model.onnx</c>, which is what makes the swap leave nothing behind.
    /// </summary>
    [Fact]
    public void EveryBuildLandsOnTheSameFile()
    {
        var folder = Directory.CreateTempSubdirectory("d47-builds").FullName;

        try
        {
            using var installer = new KokoroInstaller(folder, NullLogger<KokoroInstaller>.Instance);

            foreach (var build in KokoroAssets.Builds)
            {
                Assert.Equal(
                    Path.Combine(folder, "model.onnx"),
                    installer.Destination(build.Asset));
            }

            // And nothing else moved: a voice still nests and the dictionary still does not.
            Assert.Equal(
                Path.Combine(folder, "voices", "af_heart.bin"),
                installer.Destination(KokoroAssets.Voices.First(v => v.Path.EndsWith("af_heart.bin"))));

            Assert.Equal(
                Path.Combine(folder, "phoneme_dict.json"),
                installer.Destination(KokoroAssets.Dictionary));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---- Which build is actually there --------------------------------------------------------

    /// <summary>
    /// Read from the file rather than from settings, so a Commander who replaced <c>model.onnx</c> by
    /// hand is told what they have rather than what d47 last wrote down.
    /// </summary>
    [Fact]
    public void TheInstalledBuildIsReadFromTheFilesOwnSize()
    {
        var folder = Directory.CreateTempSubdirectory("d47-installed").FullName;

        try
        {
            var model = Path.Combine(folder, "model.onnx");

            // Nothing there at all.
            Assert.Null(KokoroAssets.InstalledBuild(folder));

            // A build's exact size is that build.
            var wanted = KokoroAssets.BuildFor("uint8");
            Grow(model, wanted.Asset.Bytes);

            Assert.Equal("uint8", KokoroAssets.InstalledBuild(folder)?.Id);

            // A size none of the eight has is a build from a different version of the repository, and is
            // reported as unknown rather than guessed at.
            Grow(model, 1234);
            Assert.Null(KokoroAssets.InstalledBuild(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// And a switch onto the build already installed is answered without a download — which is what
    /// stops re-selecting the current row costing 300 MB.
    /// </summary>
    [Fact]
    public async Task SwitchingToTheBuildAlreadyThereFetchesNothing()
    {
        var folder = Directory.CreateTempSubdirectory("d47-switch").FullName;

        try
        {
            Grow(Path.Combine(folder, "model.onnx"), KokoroAssets.BuildFor("q4f16").Asset.Bytes);

            using var installer = new KokoroInstaller(folder, NullLogger<KokoroInstaller>.Instance);

            var result = await installer.SwitchAsync(
                "q4f16", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(KokoroInstall.AlreadyPresent, result.Outcome);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A file of exactly this many bytes, without writing that many.</summary>
    private static void Grow(string path, long bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        file.SetLength(bytes);
    }
}
