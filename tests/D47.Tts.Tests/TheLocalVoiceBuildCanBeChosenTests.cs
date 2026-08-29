using D47.Core.Speech;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// Kokoro's eight published builds, offered with a measured speed beside each size
/// (<a href="https://github.com/dseelinger/d47/issues/139">#139</a>).
/// <para>
/// <b>325 MB is a lot to ask of somebody on a slow connection or a small drive</b>, and 0.84.0
/// pinned fp32 for everybody. The repository publishes seven other builds, and the interesting part
/// is why they could not simply be listed: <b>size does not predict speed here, and it points the
/// wrong way.</b> fp32 was the fastest of the three ever benched and the smallest was four times
/// slower than the largest.
/// </para>
/// <para>
/// The five unmeasured ones were measured on 2026-08-29 before any of them was offered, and the
/// answer reordered the list again — <c>uint8</c> is the fastest of the eight at half fp32's size.
/// This file guards the facts that ranking rests on, and the two rules that keep a Commander's
/// drive and voice intact while they experiment.
/// </para>
/// </summary>
public class TheLocalVoiceBuildCanBeChosenTests
{
    // ---- What the picker is allowed to offer --------------------------------------------------

    /// <summary>
    /// <b>Every build offered has been measured</b>, which is the acceptance test written as an
    /// assertion: no build reaches the picker on its file size alone.
    /// </summary>
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

    /// <summary>
    /// <b>A pinned hash per build, since each is a different file.</b> The hash and the bytes come
    /// from the same server, so anything able to serve different bytes could serve the hash for
    /// them — which is why these are in the build rather than asked for on the day.
    /// </summary>
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

    /// <summary>
    /// <b>The eight sizes are distinct, and that is load-bearing rather than incidental.</b>
    /// <see cref="KokoroAssets.InstalledBuild"/> tells them apart by byte count, because every build
    /// lands on disk under the same name — so two builds sharing a size would make the installed one
    /// unknowable.
    /// </summary>
    [Fact]
    public void NoTwoBuildsAreTheSameSize() =>
        Assert.Equal(
            KokoroAssets.Builds.Count,
            KokoroAssets.Builds.Select(build => build.Asset.Bytes).Distinct().Count());

    /// <summary>
    /// <b>The label carries speed and size together, never one without the other.</b> That is the
    /// whole point of the row: a list of eight builds by file size would tell a Commander almost
    /// nothing true about what they were choosing.
    /// </summary>
    [Fact]
    public void EveryLabelSaysBothSizeAndSpeed()
    {
        foreach (var build in KokoroAssets.Builds)
        {
            Assert.Contains("MB", build.Label, StringComparison.Ordinal);
            Assert.Contains("realtime", build.Label, StringComparison.Ordinal);
            Assert.StartsWith(build.Id, build.Label, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>The measurement's headline, pinned so a later edit cannot quietly reverse it.</b> The
    /// smallest build is not the fastest and the largest is not the slowest — if either of those
    /// ever becomes true here it is because somebody typed a number, not because the model changed.
    /// </summary>
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
    /// <b>fp32 remains the default.</b> #139 adds a choice; it does not reopen the one already
    /// made — so a Commander who never opens this row hears exactly what 0.84.0 gave them.
    /// </summary>
    [Fact]
    public void TheDefaultIsStillFp32()
    {
        Assert.Equal("fp32", KokoroAssets.DefaultBuildId);
        Assert.Equal("fp32", KokoroAssets.Builds[0].Id);
        Assert.Equal(KokoroAssets.Builds[0].Asset, KokoroAssets.Model);
    }

    /// <summary>
    /// A name this version does not publish resolves to the default rather than failing, which is
    /// what every other stored id in this repository does — and is what stops a settings file
    /// written by a later build from silencing an earlier one.
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
    /// <b>Every build lands as <c>model.onnx</c>, which is what makes the swap leave nothing
    /// behind.</b> Eight builds accumulating beside each other would be 1.4 GB of a Commander's
    /// drive spent by a row they were experimenting with — so there is one model file and the
    /// switch overwrites it.
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
    /// <b>Read from the file rather than from settings</b>, so a Commander who replaced
    /// <c>model.onnx</c> by hand is told what they have rather than what d47 last wrote down.
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

            // A build's exact size is that build. Sparse, so this costs no disk: the check is a
            // stat and never a read, which is the reason it can run every time a row is drawn.
            var wanted = KokoroAssets.BuildFor("uint8");
            Grow(model, wanted.Asset.Bytes);

            Assert.Equal("uint8", KokoroAssets.InstalledBuild(folder)?.Id);

            // A size none of the eight has is a build from a different version of the repository,
            // and is reported as unknown rather than guessed at.
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
