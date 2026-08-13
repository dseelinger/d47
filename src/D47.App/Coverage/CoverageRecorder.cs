using System.Text.Json;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Coverage;
using Microsoft.Extensions.Logging;

namespace D47.App.Coverage;

/// <summary>
/// Notes which tools and settings rows have actually been driven in the running app, so
/// "have I exercised this by hand, and has it changed since I did" has an answer.
/// <para>
/// <b>Off unless asked for.</b> This is a workbench aid for whoever builds d47, not a feature a
/// Commander wants, so it is gated on the <c>D47_COVERAGE</c> environment variable and does
/// nothing at all — no file, no hooks, no panel row — when that is unset. Which is why it is an
/// environment variable and not a setting: settings are documented, voice-reachable and visible
/// to everyone, and this should be none of those things.
/// </para>
/// <para>
/// <b>It is not telemetry.</b> Nothing is transmitted. The file sits in <c>data/</c> beside the
/// executable like everything else d47 writes, and records tool and settings-row identifiers —
/// never arguments, values, or anything the Commander said or typed.
/// </para>
/// </summary>
public sealed class CoverageRecorder
{
    /// <summary>Set this to <c>1</c> to turn recording and the panel row on.</summary>
    public const string EnvironmentVariable = "D47_COVERAGE";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _file;
    private readonly string _reportFile;
    private readonly Func<DateTimeOffset> _now;
    private readonly ILogger<CoverageRecorder> _logger;
    private readonly CoverageLedger _ledger;

    private IReadOnlyList<CoverageItem> _inventory = [];

    private CoverageRecorder(
        AppPaths paths,
        Func<DateTimeOffset> now,
        ILogger<CoverageRecorder> logger,
        CoverageLedger ledger)
    {
        _file = Path.Combine(paths.Data, "coverage.json");
        _reportFile = Path.Combine(paths.Data, "coverage.md");
        _now = now;
        _logger = logger;
        _ledger = ledger;
    }

    /// <summary>Whether recording is switched on for this process.</summary>
    public static bool Enabled =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>Where the readable report is written.</summary>
    public string ReportPath => _reportFile;

    /// <summary>
    /// A recorder if this process asked for one, otherwise null — so every caller's check is
    /// "is there one", and there is no disabled object quietly doing nothing.
    /// </summary>
    public static CoverageRecorder? Create(
        AppPaths paths,
        Func<DateTimeOffset> now,
        ILogger<CoverageRecorder> logger)
    {
        if (!Enabled)
        {
            return null;
        }

        logger.LogInformation(
            "Coverage recording is on; {File}", Path.Combine(paths.Data, "coverage.json"));

        return Regardless(paths, now, logger);
    }

    /// <summary>
    /// A recorder without consulting the environment, so a test can exercise the wiring without
    /// setting a process-wide variable that every other test in the run would also see.
    /// </summary>
    internal static CoverageRecorder Regardless(
        AppPaths paths,
        Func<DateTimeOffset> now,
        ILogger<CoverageRecorder> logger)
    {
        var file = Path.Combine(paths.Data, "coverage.json");

        return new CoverageRecorder(paths, now, logger, Load(file, logger));
    }

    /// <summary>
    /// Takes the inventory and starts listening. The registry is the inventory's source, and
    /// the settings service is where a row being changed shows up.
    /// </summary>
    public void Follow(CapabilityRegistry registry, SettingsService settings)
    {
        _inventory = CoverageInventory.Of(registry);

        registry.ToolInvoked += invocation => Record(
            CoverageKind.Tool,
            invocation.Tool,
            invocation.Succeeded ? CoverageOutcome.Ok : CoverageOutcome.Failed);

        // Applied rather than Changed: a row set to the value it already had, or refused a bad
        // value, was still driven by hand, and Changed does not fire for either. Changed also
        // never fires when a save fails, which is the one outcome most worth seeing here.
        settings.Applied += applied =>
        {
            if (OutcomeOf(applied.Status) is { } outcome)
            {
                Record(CoverageKind.Setting, applied.Key, outcome);
            }
        };

        _logger.LogInformation("Coverage: {Summary}", Report().Summary);
    }

    /// <summary>Where everything stands right now.</summary>
    public CoverageReport Report() => _ledger.Report(_inventory);

    /// <summary>
    /// Writes the ledger and the readable report. Called at shutdown rather than on every
    /// record: this is a file a person opens between sessions, not a live view.
    /// </summary>
    public void Save()
    {
        try
        {
            if (_ledger.Dirty)
            {
                File.WriteAllText(_file, JsonSerializer.Serialize(_ledger.Marks, Json));
                _ledger.Saved();
            }

            File.WriteAllText(_reportFile, Report().ToMarkdown(_now()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A workbench aid must never be why a shutdown fails.
            _logger.LogWarning(ex, "Could not write the coverage record");
        }
    }

    /// <summary>
    /// How an apply reads as coverage, or null when it is not evidence of anything.
    /// </summary>
    private static CoverageOutcome? OutcomeOf(SettingApplyStatus status) => status switch
    {
        // The change was valid and could not be saved. The one outcome worth going back to.
        SettingApplyStatus.Failed => CoverageOutcome.Failed,

        // Refused is the protected rule holding, which means the model can never be evidence
        // that a protected row has been driven — only the panel, a hotkey or the keyword
        // router can be. Marking it green would report a row as tested that nobody has
        // touched. UnknownKey names no row at all.
        SettingApplyStatus.Refused or SettingApplyStatus.UnknownKey => null,

        // Applied, Unchanged and Rejected all mean the row was driven and answered. Rejected
        // is validation working, the same reading as a refused tool argument.
        _ => CoverageOutcome.Ok,
    };

    private void Record(string kind, string id, CoverageOutcome outcome)
    {
        var item = _inventory.FirstOrDefault(i => i.Kind == kind && i.Id == id);

        if (item is not null)
        {
            _ledger.Record(item, _now(), outcome);
        }
    }

    private static CoverageLedger Load(string file, ILogger logger)
    {
        try
        {
            if (File.Exists(file))
            {
                var marks = JsonSerializer.Deserialize<Dictionary<string, CoverageMark>>(
                    File.ReadAllText(file));

                if (marks is not null)
                {
                    return new CoverageLedger(marks);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Starting over is the right failure: the record is an aid, and a corrupt one that
            // stopped the app would be worse than no record at all.
            logger.LogWarning(ex, "Could not read the coverage record; starting a new one");
        }

        return new CoverageLedger();
    }
}
