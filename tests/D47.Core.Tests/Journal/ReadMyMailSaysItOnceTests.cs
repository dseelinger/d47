using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The read_mail tool answers from the mail ledger and marks what it said as read (#619).</summary>
[Trait("Category", "Integration")]
public sealed class ReadMyMailSaysItOnceTests : IDisposable
{
    private const string Doug = "F1";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-readmail-" + Guid.NewGuid().ToString("N"));

    public ReadMyMailSaysItOnceTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private MailLedger Ledger(bool folded = true)
    {
        var ledger = new MailLedger(Path.Combine(_folder, MailLedger.FileName), NullLogger.Instance);

        if (folded)
        {
            ledger.FoldHistory([], TestContext.Current.CancellationToken);
        }

        return ledger;
    }

    private static void Complete(MailLedger ledger, long mission, string at)
    {
        var line = $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"MissionCompleted","Name":"Mission_Delivery","MissionID":{{mission}},"Reward":1000}""";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        ledger.Fold([parsed!], live: true, Doug);
    }

    private static async Task<string> Ask(MailLedger ledger, string? commander = Doug)
    {
        var registry = CapabilityRegistry.Build([CommsCapability.Create(ActionSurface.Inert, () => false, ledger, () => commander)]);
        var result = await registry.InvokeAsync("read_mail", new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        return result.Content;
    }

    [Fact]
    public async Task AskingAgainAfterTheMailWasReadFindsNothingNew()
    {
        var ledger = Ledger();
        Complete(ledger, 1, "10:00:00");

        Assert.Contains("one mission completed", await Ask(ledger), StringComparison.Ordinal);
        Assert.StartsWith("Nothing I know of", await Ask(ledger), StringComparison.Ordinal);

        Complete(ledger, 2, "10:05:00");

        Assert.Contains("one mission completed", await Ask(ledger), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMailIsNotReadWhileTheJournalsAreStillBeingWalked()
    {
        var ledger = Ledger(folded: false);
        Complete(ledger, 1, "10:00:00");

        Assert.StartsWith("I'm still reading", await Ask(ledger), StringComparison.Ordinal);
        Assert.Null(ledger.ReadThrough(Doug));
    }

    [Fact]
    public async Task WithNoCommanderYetItSaysSoAndReadsNothing()
    {
        var ledger = Ledger();
        Complete(ledger, 1, "10:00:00");

        Assert.StartsWith("I don't know which Commander", await Ask(ledger, commander: null), StringComparison.Ordinal);
        Assert.Null(ledger.ReadThrough(Doug));
    }

    [Fact]
    public void ThePhrasesForMailNeedNoModel()
    {
        var tool = CommsCapability.Create(ActionSurface.Inert, () => false).Tools.Single(t => t.Name == "read_mail");

        Assert.False(tool.Protected);
        Assert.Equal(
            ["read my mail", "any mail", "check my messages"],
            tool.Commands.Select(c => c.Phrase));
    }
}
