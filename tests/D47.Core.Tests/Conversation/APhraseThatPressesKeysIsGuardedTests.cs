using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

public sealed class APhraseThatPressesKeysIsGuardedTests
{
    [Fact]
    public void EveryPhraseAlreadyTakenIsInTheBook()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var offer = new ClipboardOffer();
        offer.Offer("Cubeo", "the system");
        var dynamic = offer.Phrases().ToList();

        string[] taken =
        [
            .. registry.All.SelectMany(c => c.Descriptor.Keywords).Select(keyword => keyword.Phrase),
            .. registry.All.SelectMany(c => c.Descriptor.InterruptKeywords),
            .. registry.All.SelectMany(c => c.Descriptor.Tools).SelectMany(t => t.Commands)
                .Select(command => command.Phrase),
            .. registry.All.SelectMany(c => c.Descriptor.Settings).SelectMany(row => row.Commands)
                .Select(command => command.Phrase),
            .. dynamic.Select(command => command.Phrase),
            .. ClipboardOffer.EveryPhrase,
            CommunityGoalCourse.SetCourse,
        ];

        var book = PhraseBook.From(registry, dynamic).Entries.Select(entry => entry.Phrase).ToHashSet();

        Assert.NotEmpty(taken);
        Assert.All(taken, phrase => Assert.Contains(phrase, book));
    }

    [Fact]
    public void PuttingTheGearDownIsGuarded()
    {
        using var install = new TempInstall();

        var entry = Assert.Single(
            PhraseBook.From(TestSurface.For(install).Registry, []).Entries,
            entry => entry.Phrase == "put the gear down");

        Assert.Equal("control_flight", entry.ToolName);
        Assert.True(entry.Guarded);
    }

    [Fact]
    public void SettingFocusToEliteIsGuarded()
    {
        using var install = new TempInstall();

        var entry = Assert.Single(
            PhraseBook.From(TestSurface.For(install).Registry, []).Entries,
            entry => entry.Phrase == "set focus to elite");

        Assert.Equal("focus_the_game", entry.ToolName);
        Assert.True(entry.Guarded);
    }

    [Fact]
    public void CopyThatWithAnOfferStandingIsNotGuarded()
    {
        using var install = new TempInstall();

        var offer = new ClipboardOffer();
        offer.Offer("Cubeo", "the system");

        var entry = Assert.Single(
            PhraseBook.From(TestSurface.For(install).Registry, offer.Phrases()).Entries,
            entry => entry.Phrase == "copy that");

        Assert.Equal("copy_to_clipboard", entry.ToolName);
        Assert.Equal("Cubeo", entry.Arguments["text"]);
        Assert.False(entry.Guarded);
    }

    [Theory]
    [InlineData("control_flight", true)]
    [InlineData("control_systems", true)]
    [InlineData("control_interface", true)]
    [InlineData("control_srv", true)]
    [InlineData("ship_command", true)]
    [InlineData("run_macro", true)]
    [InlineData("plot_course", true)]
    [InlineData("send_chat_message", true)]
    [InlineData("report_switches", false)]
    [InlineData("list_macros", false)]
    [InlineData("copy_to_clipboard", false)]
    public void OnlyTheToolsThatReachTheKeyboardSendInput(string name, bool sendsInput)
    {
        using var install = new TempInstall();

        var tool = Assert.Single(
            TestSurface.For(install).Registry.All.SelectMany(c => c.Descriptor.Tools),
            tool => tool.Name == name);

        Assert.Equal(sendsInput, tool.SendsInput);
    }

    [Fact]
    public void AMacroNamedForAPhraseACapabilityOwnsIsStillRefused()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var file = Path.Combine(install.Paths.Data, "phrase-book-macros.json");
        File.WriteAllText(file, """
        { "macros": [ { "name": "put the gear down", "steps": [ { "action": "lights" } ] } ] }
        """);

        var store = new MacroStore(file, NullLogger<MacroStore>.Instance);
        store.Poll([.. PhraseBook.From(registry, []).Entries.Select(entry => entry.Phrase)]);

        Assert.Empty(store.Macros);
        Assert.Contains("already a command", Assert.Single(store.Problems).Reason, StringComparison.OrdinalIgnoreCase);
    }
}
