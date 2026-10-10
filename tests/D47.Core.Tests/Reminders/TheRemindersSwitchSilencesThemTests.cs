using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>One switch, Reminders, default on.</summary>
public class TheRemindersSwitchSilencesThemTests
{
    [Fact]
    public void SwitchedOffNothingIsSaidAndNothingIsFired()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextDocking, "Buy limpets.");

        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(bench.Callout);
        engine.SetEnabled(bench.Callout.Id, false);

        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var dock = ReminderBench.Event("Docked", ("StationName", "Jameson Memorial"));
        state.Apply(dock);

        engine.Tick(new CalloutContext(ReminderBench.Now, false, state, GameStatus.Unknown, NavRoute.None, [dock]));

        Assert.Empty(engine.Drain());
        Assert.Equal(JournalReminderState.Armed, Assert.Single(bench.Store.For("F1")).State);

        engine.SetEnabled(bench.Callout.Id, true);
        engine.Tick(new CalloutContext(ReminderBench.Now, false, state, GameStatus.Unknown, NavRoute.None, [dock]));

        Assert.Single(engine.Drain());
    }

    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.RemindersKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(new CalloutSettings().Reminders);
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));
        Assert.False(row.Binding!.Write!(D47Settings.Defaults, "false")!.Callouts.Reminders);
    }
}
