using System.Diagnostics.CodeAnalysis;
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
        Assert.Equal(2, before.Total);
        Assert.Equal(2, before.Never);

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

    private static CoverageStatus StatusOf(CoverageReport report, string kind, string id) =>
        report.Lines.Single(line => line.Item.Kind == kind && line.Item.Id == id).Status;

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
