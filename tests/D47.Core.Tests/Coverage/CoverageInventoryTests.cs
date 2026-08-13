using D47.Core.Capabilities;
using D47.Core.Tests;
using D47.Core.Coverage;
using Xunit;

namespace D47.Core.Tests.Coverage;

/// <summary>
/// The inventory is derived from the registry rather than maintained beside it, so a capability
/// added in a later phase shows up as never-exercised the day it is written and nobody has to
/// remember to list it.
/// </summary>
public class CoverageInventoryTests
{
    [Fact]
    public void EveryToolAndEverySettingsRowIsInTheInventory()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var inventory = CoverageInventory.Of(registry);

        var tools = inventory.Where(i => i.Kind == CoverageKind.Tool).Select(i => i.Id).ToList();
        var rows = inventory.Where(i => i.Kind == CoverageKind.Setting).Select(i => i.Id).ToList();

        Assert.Equal([.. registry.ToolNames.Order()], [.. tools.Order()]);

        Assert.Equal(
            [.. registry.All.SelectMany(c => c.Descriptor.Settings.Select(r => r.Key)).Order()],
            [.. rows.Order()]);
    }

    /// <summary>
    /// Every line links to its capability's help page, and the id is what builds that URL. An
    /// item that could not name its capability would be a line with nowhere to go.
    /// </summary>
    [Fact]
    public void EveryItemNamesTheCapabilityItCameFrom()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var inventory = CoverageInventory.Of(registry);
        var known = registry.All.Select(c => c.Descriptor.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(inventory, item => Assert.Contains(item.CapabilityId, known));

        // And it is the capability that actually declared it, not merely some real id.
        var declared = registry.All
            .SelectMany(c => c.Descriptor.Tools.Select(t => (Id: t.Name, Capability: c.Descriptor.Id)))
            .ToDictionary(x => x.Id, x => x.Capability, StringComparer.Ordinal);

        Assert.All(
            inventory.Where(i => i.Kind == CoverageKind.Tool),
            item => Assert.Equal(declared[item.Id], item.CapabilityId));
    }

    /// <summary>
    /// The capability id is identity, not definition. Folding it into the fingerprint would
    /// have marked every already-recorded item stale the day it was added, telling a Commander
    /// to re-test the whole app for a change nothing about the app actually made.
    /// </summary>
    [Fact]
    public void TheCapabilityIdIsNotPartOfTheFingerprint()
    {
        static string FingerprintUnder(string capabilityId) =>
            CoverageInventory.Of(CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = capabilityId,
                    Group = "Foundation",
                    Name = "Probe",
                    Summary = "A capability that exists to carry one tool.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "probe_tool",
                            Description = "Does nothing, observably.",
                            Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
                        },
                    ],
                },
            ])).Single().Fingerprint;

        Assert.Equal(FingerprintUnder("probe"), FingerprintUnder("somewhere-else"));
    }

    /// <summary>Nothing is exercised on a machine that has never run d47.</summary>
    [Fact]
    public void AFreshLedgerReportsTheWholeAppAsUntouched()
    {
        using var install = new TempInstall();
        var inventory = CoverageInventory.Of(TestSurface.For(install).Registry);

        var report = new CoverageLedger().Report(inventory);

        Assert.Equal(report.Total, report.Never);
        Assert.True(report.Total > 50, "the inventory covers the whole registered surface");
    }

    /// <summary>
    /// Two builds of the same code fingerprint identically, or every item would read as changed
    /// on every launch and the report would be noise.
    /// </summary>
    [Fact]
    public void TheSameDefinitionFingerprintsTheSameAcrossBuilds()
    {
        using var install = new TempInstall();

        var first = CoverageInventory.Of(TestSurface.For(install).Registry);
        var second = CoverageInventory.Of(TestSurface.For(install).Registry);

        Assert.Equal(
            first.Select(i => (i.Key, i.Fingerprint)),
            second.Select(i => (i.Key, i.Fingerprint)));
    }

    /// <summary>
    /// A row whose wording changed is a row worth looking at again, because the wording is most
    /// of what a Commander judges it by.
    /// </summary>
    [Fact]
    public void RewordingARowChangesItsFingerprint()
    {
        var before = Row("Held, d47 listens.");
        var after = Row("Hold this and d47 listens.");

        Assert.NotEqual(Fingerprint(before), Fingerprint(after));
    }

    /// <summary>
    /// Eleven of d47's tools take no arguments, and the canonical schema writer emits only
    /// parameters — so schema alone would fingerprint them all the same and staleness would
    /// silently never fire for most of the tool surface.
    /// </summary>
    [Fact]
    public void ZeroArgumentToolsStillFingerprintDistinctly()
    {
        using var install = new TempInstall();

        var tools = CoverageInventory.Of(TestSurface.For(install).Registry)
            .Where(item => item.Kind == CoverageKind.Tool)
            .ToList();

        Assert.Equal(tools.Count, tools.Select(item => item.Fingerprint).Distinct().Count());
    }

    /// <summary>A tool the model now reads differently is a tool worth trying again.</summary>
    [Fact]
    public void RewordingAToolsDescriptionChangesItsFingerprint()
    {
        Assert.NotEqual(
            ToolFingerprint("Report where d47 keeps its files."),
            ToolFingerprint("Say where d47 keeps its files."));
    }

    private static string ToolFingerprint(string description) =>
        CoverageInventory.Of(CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "probe",
                Group = "Foundation",
                Name = "Probe",
                Summary = "A capability that exists to carry one tool.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "probe_tool",
                        Description = description,
                        Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
                    },
                ],
            },
        ])).Single().Fingerprint;

    private static SettingRow Row(string help) => new()
    {
        Key = "listening.pushToTalkKey",
        Label = "Push-to-talk key",
        Help = help,
        Kind = SettingKind.Hotkey,
        Binding = new SettingBinding { Read = _ => null },
    };

    private static string Fingerprint(SettingRow row) =>
        CoverageInventory.Of(CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "probe",
                Group = "Foundation",
                Name = "Probe",
                Summary = "A capability that exists to carry one row.",
                Settings = [row],
            },
        ])).Single().Fingerprint;
}
