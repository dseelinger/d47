using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Diagnostics.Flight;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// The audio flight recorder's ring
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// Three properties, and every one of them is a promise rather than a nicety. The cap is enforced
/// by the writer, so the folder is under it at every moment nothing is being written. A kept row
/// is never evicted, because outliving the rolling window is what keeping means. And the wipe
/// takes everything, kept clips included — a wipe that spared them would leave the Commander's
/// voice on disk under a button saying it had been deleted.
/// </para>
/// </summary>
public class TheRecordingHasACapTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-flight-recorder", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// A tiny cap, so eviction is asserted rather than assumed. The real one is two hundred
    /// megabytes and proving it at full size would mean writing two hundred megabytes per run.
    /// </summary>
    private const long SmallCap = 200_000;

    private FlightLog Log(long cap = SmallCap) => new(_folder, NullLogger.Instance, cap);

    /// <summary>
    /// One clip, sized so that a handful of them crosses the cap without the test writing two
    /// hundred megabytes. Sixteen bits per sample at 16 kHz is the heard side's real shape.
    /// </summary>
    private static FlightCapture Capture(
        int minute,
        FlightDirection direction = FlightDirection.Heard,
        string text = "hello",
        int samples = 16_000) =>
        new(direction, Noon.AddMinutes(minute), WavWriter.ToBytes(new float[samples], 16_000), TimeSpan.FromSeconds(1))
        {
            Text = text,
        };

    [Fact]
    public void A_row_survives_a_restart()
    {
        var written = Log().Add(Capture(0, text: "set course for Colonia"));

        var reread = Log().Rows;

        var row = Assert.Single(reread);
        Assert.Equal(written.Id, row.Id);
        Assert.Equal("set course for Colonia", row.Text);
        Assert.True(File.Exists(Path.Combine(_folder, row.Clip)));
    }

    /// <summary>
    /// Two rows landing in the same millisecond in opposite directions do not fight over one
    /// file name. The tap and the transcriber run on different threads and both write here.
    /// </summary>
    [Fact]
    public void Two_directions_in_one_millisecond_get_two_clips()
    {
        var log = Log();

        var heard = log.Add(Capture(0, FlightDirection.Heard));
        var said = log.Add(Capture(0, FlightDirection.Spoken));

        Assert.NotEqual(heard.Id, said.Id);
        Assert.Equal(2, log.Rows.Count);
    }

    [Fact]
    public void The_oldest_goes_when_the_cap_is_crossed()
    {
        var log = Log();

        // One clip a little over a tenth of the cap, so eleven of them cannot all be held.
        var big = (int)(SmallCap / 2 / 10) + 1_000;

        for (var minute = 0; minute < 11; minute++)
        {
            log.Add(Capture(minute, text: $"utterance {minute}", samples: big));
        }

        var rows = log.Rows;

        Assert.True(rows.Sum(row => row.Bytes) <= SmallCap);
        Assert.DoesNotContain(rows, row => row.Text == "utterance 0");
        Assert.Contains(rows, row => row.Text == "utterance 10");

        // Enforced by the writer, not by the index: the evicted clip is gone from disk too.
        Assert.Equal(
            rows.Count,
            Directory.EnumerateFiles(_folder, "*.wav").Count());
    }

    [Fact]
    public void A_kept_row_is_never_evicted()
    {
        var log = Log();
        var big = (int)(SmallCap / 2 / 10) + 1_000;

        var first = log.Add(Capture(0, text: "the one that matters", samples: big));

        Assert.NotNull(log.Keep(first.Id, FlightKeepKind.Mishear, "the one that mattered", Noon));

        for (var minute = 1; minute < 12; minute++)
        {
            log.Add(Capture(minute, text: $"utterance {minute}", samples: big));
        }

        Assert.Contains(log.Rows, row => row.Text == "the one that matters");
    }

    /// <summary>
    /// The half a recording cannot supply is written down beside it, in the corpus its kind
    /// belongs to. This is what makes a kept row re-runnable rather than a souvenir.
    /// </summary>
    [Fact]
    public void A_kept_mishear_joins_the_mishear_corpus()
    {
        var log = Log();
        var row = log.Add(Capture(0, text: "set course for Colonel"));

        var kept = log.Keep(row.Id, FlightKeepKind.Mishear, "set course for Colonia", Noon);

        Assert.NotNull(kept);
        Assert.Equal(FlightKeepKind.Mishear, kept.Kept!.Kind);

        var corpus = Path.Combine(_folder, FlightLog.KeptFolderName, "mishears.json");

        Assert.True(File.Exists(corpus));
        Assert.True(File.Exists(Path.Combine(_folder, FlightLog.KeptFolderName, row.Clip)));

        using var read = JsonDocument.Parse(File.ReadAllText(corpus));
        var entry = Assert.Single(read.RootElement.EnumerateArray());

        Assert.Equal("set course for Colonel", entry.GetProperty("Text").GetString());
        Assert.Equal("set course for Colonia", entry.GetProperty("Expected").GetString());
        Assert.Equal(row.Clip, entry.GetProperty("Clip").GetString());
    }

    /// <summary>
    /// A mispronunciation is the other corpus, and it carries the phonemes — which is the column
    /// that turns it from an anecdote into a diagnosis, and the reason the pair is a test case.
    /// </summary>
    [Fact]
    public void A_kept_mispronunciation_joins_the_other_corpus_with_its_phonemes()
    {
        var log = Log();

        var row = log.Add(new FlightCapture(
            FlightDirection.Spoken,
            Noon,
            WavWriter.ToBytes(new float[16_000], 16_000),
            TimeSpan.FromSeconds(1))
        {
            Text = "Observatory",
            Phonemes = "ɒbzɜːveɪ",
            Provider = "Kokoro (on this machine)",
            Voice = "af_heart",
        });

        log.Keep(row.Id, FlightKeepKind.Pronunciation, "əbzˈɜːvətɹi", Noon);

        var corpus = Path.Combine(_folder, FlightLog.KeptFolderName, "pronunciations.json");

        Assert.False(File.Exists(Path.Combine(_folder, FlightLog.KeptFolderName, "mishears.json")));

        using var read = JsonDocument.Parse(File.ReadAllText(corpus));
        var entry = Assert.Single(read.RootElement.EnumerateArray());

        Assert.Equal("ɒbzɜːveɪ", entry.GetProperty("Phonemes").GetString());
        Assert.Equal("əbzˈɜːvətɹi", entry.GetProperty("Expected").GetString());
    }

    /// <summary>Keeping the same row twice corrects the entry rather than filing a second one.</summary>
    [Fact]
    public void Keeping_a_row_twice_leaves_one_entry()
    {
        var log = Log();
        var row = log.Add(Capture(0, text: "set course for Colonel"));

        log.Keep(row.Id, FlightKeepKind.Mishear, "set course for Colonia", Noon);
        log.Keep(row.Id, FlightKeepKind.Mishear, "set course for Sol", Noon);

        using var read = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(_folder, FlightLog.KeptFolderName, "mishears.json")));

        var entry = Assert.Single(read.RootElement.EnumerateArray());

        Assert.Equal("set course for Sol", entry.GetProperty("Expected").GetString());
    }

    [Fact]
    public void The_wipe_takes_the_kept_clips_too()
    {
        var log = Log();
        var row = log.Add(Capture(0));

        log.Keep(row.Id, FlightKeepKind.Mishear, "anything", Noon);
        log.Empty();

        Assert.Empty(log.Rows);
        Assert.False(Directory.Exists(_folder));
        Assert.Empty(Log().Rows);
    }

    [Fact]
    public void The_summary_says_how_much_of_the_cap_is_used()
    {
        // The real cap here rather than the small one, because what this asserts is that the
        // stated number is the one the Commander is shown.
        var log = Log(FlightLog.CapBytes);

        Assert.Contains("Nothing recorded", log.Summary(), StringComparison.Ordinal);

        log.Add(Capture(0));
        log.Add(Capture(1, FlightDirection.Spoken));

        var summary = log.Summary();

        Assert.Contains("2 utterances", summary, StringComparison.Ordinal);
        Assert.Contains("1 heard", summary, StringComparison.Ordinal);
        Assert.Contains($"{SmallCap / (1024 * 1024)} MB", summary, StringComparison.Ordinal);
    }
}
