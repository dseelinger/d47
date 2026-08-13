using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using D47.App.Coverage;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Coverage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The recorder is only useful if its two hooks actually fire in the running app — invoking a
/// tool, and changing a settings row. A ledger that is correct but never told anything reports
/// a perfectly accurate zero.
/// <para>
/// Driven through a probe capability rather than the builtin registry, so this tests the wiring
/// and nothing else: it cannot start passing or failing because a real capability was renamed.
/// </para>
/// </summary>
public class CoverageRecorderTests
{
    private static readonly DateTimeOffset Monday = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InvokingAToolAndChangingARowAreBothRecorded()
    {
        var probe = new Probe();

        probe.Recorder.Follow(probe.Registry, probe.Settings);

        var before = probe.Recorder.Report();
        Assert.Equal(4, before.Total);
        Assert.Equal(4, before.Never);

        await probe.Registry.InvokeAsync(
            "probe_tool",
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);
        Assert.True(probe.Settings.Apply("probe.row", "dark", SettingsCaller.Panel).Ok);

        var after = probe.Recorder.Report();

        Assert.Equal(CoverageStatus.Exercised, StatusOf(after, CoverageKind.Tool, "probe_tool"));
        Assert.Equal(CoverageStatus.Exercised, StatusOf(after, CoverageKind.Setting, "probe.row"));
        Assert.Equal(2, after.Exercised);
    }

    /// <summary>
    /// What was exercised survives a restart, or the record would only ever cover one session —
    /// which is the whole difference between this and the session-scoped help ranking.
    /// </summary>
    [Fact]
    public async Task WhatWasExercisedSurvivesARestart()
    {
        var probe = new Probe();

        probe.Recorder.Follow(probe.Registry, probe.Settings);
        await probe.Registry.InvokeAsync(
            "probe_tool",
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);
        probe.Recorder.Save();

        // A second recorder over the same folder is what the next launch gets.
        var next = CoverageRecorder.Regardless(
            probe.Paths, () => Monday, NullLogger<CoverageRecorder>.Instance);

        next.Follow(probe.Registry, probe.Settings);

        Assert.Equal(
            CoverageStatus.Exercised,
            StatusOf(next.Report(), CoverageKind.Tool, "probe_tool"));
    }

    /// <summary>The readable report is what gets opened between sessions.</summary>
    [Fact]
    public void SavingWritesAReportAPersonCanRead()
    {
        var probe = new Probe();

        probe.Recorder.Follow(probe.Registry, probe.Settings);
        probe.Recorder.Save();

        var report = File.ReadAllText(probe.Recorder.ReportPath);

        Assert.Contains("What you have actually exercised", report, StringComparison.Ordinal);
        Assert.Contains("probe_tool", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// The outcome has to survive the whole path — registry, event, recorder, ledger — or the
    /// report shows a green line for something that threw every time it was tried.
    /// </summary>
    [Fact]
    public async Task AToolThatThrewIsRecordedAsFailed()
    {
        var probe = new Probe();
        probe.Recorder.Follow(probe.Registry, probe.Settings);

        var result = await probe.Registry.InvokeAsync(
            "probe_breaks",
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);

        var line = LineFor(probe.Recorder.Report(), CoverageKind.Tool, "probe_breaks");

        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(CoverageOutcome.Failed, line.Outcome);
    }

    /// <summary>
    /// A row set to a value it will not take was still driven by hand, and the validation
    /// saying no is the validation working — the same reading as a refused tool argument.
    /// Changed never fires for it, which is why the recorder follows Applied instead.
    /// </summary>
    [Fact]
    public void ARejectedValueStillCountsAsHavingDrivenTheRow()
    {
        var probe = new Probe();
        probe.Recorder.Follow(probe.Registry, probe.Settings);

        Assert.Equal(
            SettingApplyStatus.Rejected,
            probe.Settings.Apply("probe.protectedRow", "not a number", SettingsCaller.Panel).Status);

        var line = LineFor(probe.Recorder.Report(), CoverageKind.Setting, "probe.protectedRow");

        Assert.Equal(CoverageStatus.Exercised, line.Status);
        Assert.Equal(CoverageOutcome.Ok, line.Outcome);
    }

    /// <summary>
    /// The model can never be evidence that a protected row has been driven — only the panel, a
    /// hotkey or the keyword router can be. Recording a refusal as coverage would report a row
    /// as tested that nobody has touched, which is the one thing this record must not do.
    /// </summary>
    [Fact]
    public void ARefusedModelAttemptOnAProtectedRowIsNotCoverage()
    {
        var probe = new Probe();
        probe.Recorder.Follow(probe.Registry, probe.Settings);

        Assert.Equal(
            SettingApplyStatus.Refused,
            probe.Settings.Apply("probe.protectedRow", "2", SettingsCaller.Model).Status);

        Assert.Equal(
            CoverageStatus.Never,
            LineFor(probe.Recorder.Report(), CoverageKind.Setting, "probe.protectedRow").Status);
    }

    private static CoverageStatus StatusOf(CoverageReport report, string kind, string id) =>
        LineFor(report, kind, id).Status;

    private static CoverageLine LineFor(CoverageReport report, string kind, string id) =>
        report.Lines.Single(line => line.Item.Kind == kind && line.Item.Id == id);

    /// <summary>One capability carrying exactly one tool and one row.</summary>
    private sealed class Probe
    {
        public Probe()
        {
            Paths = new AppPaths(Directory.CreateTempSubdirectory("d47-coverage-tests").FullName);
            Paths.EnsureCreated();

            var store = new SettingsStore(Paths, NullLogger<SettingsStore>.Instance);

            Settings = new SettingsService(
                store,
                new SecretStore(Paths, new PlainProtector(), NullLogger<SecretStore>.Instance),
                store.Load(),
                NullLogger<SettingsService>.Instance);

            Registry = CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "probe",
                    Group = "Foundation",
                    Name = "Probe",
                    Summary = "A capability that exists to be exercised by a test.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "probe_tool",
                            Description = "Does nothing, observably.",
                            Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
                        },
                        new ToolDefinition
                        {
                            Name = "probe_breaks",
                            Description = "Throws, observably.",
                            Handler = (_, _) => throw new InvalidOperationException("the scoop is jammed"),
                        },
                    ],
                    Settings =
                    [
                        new SettingRow
                        {
                            Key = "probe.row",
                            Label = "A row",
                            Help = "Bound to the theme so applying it goes all the way through.",
                            Kind = SettingKind.Text,
                            Binding = new SettingBinding
                            {
                                Read = s => s.Ui.Theme,
                                Write = (s, v) => s with { Ui = s.Ui with { Theme = v ?? "elite" } },
                            },
                        },
                        new SettingRow
                        {
                            Key = "probe.protectedRow",
                            Label = "A protected row",
                            Help = "Reachable from the panel, never from the model.",
                            Kind = SettingKind.Number,
                            Protected = true,
                            Binding = new SettingBinding
                            {
                                Read = s => s.Ui.ZoomPercent.ToString(CultureInfo.InvariantCulture),
                                Write = (s, v) => s with
                                {
                                    Ui = s.Ui with
                                    {
                                        ZoomPercent = int.TryParse(v, out var percent) ? percent : 100,
                                    },
                                },
                            },
                        },
                    ],
                },
            ]);

            Settings.Bind(Registry);

            Recorder = CoverageRecorder.Regardless(
                Paths, () => Monday, NullLogger<CoverageRecorder>.Instance);
        }

        public AppPaths Paths { get; }

        public SettingsService Settings { get; }

        public CapabilityRegistry Registry { get; }

        public CoverageRecorder Recorder { get; }
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = ciphertext;
            return true;
        }
    }
}
