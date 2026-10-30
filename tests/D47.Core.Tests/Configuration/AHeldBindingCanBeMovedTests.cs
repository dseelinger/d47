using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AHeldBindingCanBeMovedTests
{
    private const string Ptt = ListeningCapability.PushToTalkKeyKey;
    private const string PttButton = ListeningCapability.PushToTalkButtonKey;
    private const string Comms = ListeningCapability.CommsPushToTalkKeyKey;
    private const string CommsButton = ListeningCapability.CommsPushToTalkButtonKey;

    [Fact]
    public void TheQuestionNamesTheKeyTheHolderAndTheNewAction()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var move = surface.Settings.MoveOf(Comms, "Scroll");

        Assert.NotNull(move);
        Assert.Equal("Scroll is already bound to Push-to-talk. Move it to Comms push-to-talk?", move.Question);
        Assert.Equal("Push-to-talk will be unbound.", move.Consequence);
    }

    [Fact]
    public void AnActionWhoseOtherHalfIsBoundSaysWhatItKeeps()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(PttButton, "stick#4", SettingsCaller.Panel);

        Assert.Equal("Push-to-talk keeps its stick button.", surface.Settings.MoveOf(Comms, "Scroll")!.Consequence);

        surface.Settings.Apply(CommsButton, "stick#7", SettingsCaller.Panel);
        surface.Settings.Apply(Comms, "Ctrl+K", SettingsCaller.Panel);

        var button = surface.Settings.MoveOf(PttButton, "stick#7");
        Assert.Equal("Comms push-to-talk keeps its key.", button!.Consequence);
        Assert.EndsWith("is already bound to Comms push-to-talk. Move it to Push-to-talk?", button.Question, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingLeavesTheKeyOnTheNewActionAndNothingOnTheOldInOneSave()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var seen = new List<(string? Ptt, string? Comms)>();
        surface.Settings.Changed += _ => seen.Add((surface.Settings.Read(Ptt), surface.Settings.Read(Comms)));

        var result = surface.Settings.Move(Comms, "Scroll", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.Null(surface.Settings.Read(Ptt));
        Assert.Equal("Scroll", surface.Settings.Read(Comms));
        Assert.NotEmpty(seen);
        Assert.All(seen, state => Assert.Equal((null, "Scroll"), (state.Ptt, state.Comms)));
    }

    [Fact]
    public void AMoveTheNewRowRefusesWritesNothing()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(InterfaceCapability.FocusAskHotkeyKey, "Ctrl+L", SettingsCaller.Panel);
        var system = SystemWideHotkey(surface.Settings);
        Assert.NotNull(system);

        surface.Settings.Apply(Comms, "F9", SettingsCaller.Panel);
        var move = surface.Settings.MoveOf(system, "F9");

        Assert.NotNull(move?.Refusal);
        Assert.Equal(SettingApplyStatus.Rejected, surface.Settings.Move(system, "F9", SettingsCaller.Panel).Status);
        Assert.Equal("F9", surface.Settings.Read(Comms));
    }

    [Fact]
    public void TheModelCannotMoveABinding()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Move(Comms, "Scroll", SettingsCaller.Model);

        Assert.NotEqual(SettingApplyStatus.Applied, result.Status);
        Assert.Equal("Scroll", surface.Settings.Read(Ptt));
    }

    [Fact]
    public void ASavedSetupMovesKeysBothWays()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(Comms, "Ctrl+K", SettingsCaller.Panel);
        var choices = SetupChoices.From(surface.Settings.Current);

        var swapped = choices with { TalkKey = "Ctrl+K", CommsKey = "Scroll" };
        Assert.Empty(FirstRun.Apply(surface.Settings, swapped));
        Assert.Equal("Ctrl+K", surface.Settings.Read(Ptt));
        Assert.Equal("Scroll", surface.Settings.Read(Comms));

        var taken = swapped with { CommsKey = null, TalkKey = "Scroll" };
        Assert.Empty(FirstRun.Apply(surface.Settings, taken));
        Assert.Equal("Scroll", surface.Settings.Read(Ptt));
        Assert.Null(surface.Settings.Read(Comms));
    }

    [Fact]
    public void ASetupKeyHeldOutsideTheWizardIsTakenOnlyWhenItSaves()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var choices = SetupChoices.From(surface.Settings.Current) with { TalkKey = "Ctrl+L" };

        Assert.Equal("Ctrl+L", surface.Settings.Read(InterfaceCapability.FocusAskHotkeyKey));

        Assert.Empty(FirstRun.Apply(surface.Settings, choices));
        Assert.Null(surface.Settings.Read(InterfaceCapability.FocusAskHotkeyKey));
        Assert.Equal("Ctrl+L", surface.Settings.Read(Ptt));
    }

    [Fact]
    public void ADraftGivesCommsTheKeyTheWizardTookOffPushToTalk()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var choices = SetupChoices.From(surface.Settings.Current) with { TalkKey = "Ctrl+K" };

        Assert.Null(surface.Settings.MoveOf(Comms, "Scroll", FirstRun.Draft(surface.Settings, choices)));
        Assert.NotNull(surface.Settings.MoveOf(Comms, "Ctrl+K", FirstRun.Draft(surface.Settings, choices)));
    }

    private static string? SystemWideHotkey(SettingsService settings) =>
        settings.Sections.SelectMany(section => section.Rows)
            .FirstOrDefault(row => row.Kind == SettingKind.Hotkey && row.SystemWide)?.Key;
}
