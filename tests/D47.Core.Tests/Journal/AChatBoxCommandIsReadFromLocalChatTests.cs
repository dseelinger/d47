using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AChatBoxCommandIsReadFromLocalChatTests
{
    private static JournalEvent Event(string kind, string to, string message) =>
        new(
            DateTimeOffset.UnixEpoch,
            kind,
            JsonDocument.Parse(JsonSerializer.Serialize(new { @event = kind, To = to, Message = message })).RootElement);

    private static string? Command(string to, string message) =>
        ChatBoxCommands.Read([Event("SendText", to, message)]).SingleOrDefault();

    [Theory]
    [InlineData("/d47 jump range", "jump range")]
    [InlineData("/D47 Jump range", "Jump range")]
    [InlineData("/d47   where am I  ", "where am I")]
    public void ALocalMessageWithThePrefixIsTheRestOfTheMessage(string message, string expected) =>
        Assert.Equal(expected, Command("local", message));

    [Theory]
    [InlineData("/d47")]
    [InlineData("/d47   ")]
    [InlineData("/d47jump range")]
    [InlineData("jump range")]
    public void ALocalMessageWithoutThePrefixAndACommandIsNoCommand(string message) =>
        Assert.Null(Command("local", message));

    [Theory]
    [InlineData("wing")]
    [InlineData("squadron")]
    [InlineData("Fixture Vex")]
    public void AMessageToAnyOtherChannelIsNeverACommand(string to) =>
        Assert.Null(Command(to, "/d47 jump range"));

    [Fact]
    public void EventsOfAnyOtherKindAreSkipped() =>
        Assert.Empty(ChatBoxCommands.Read([Event("ReceiveText", "local", "/d47 jump range")]));

    [Fact]
    public void SendTextIsListedAsActedOn() =>
        Assert.Contains("SendText", (IReadOnlySet<string>)HandledEvents.ActedOn);

    [Theory]
    [InlineData(ToolCaller.Commander)]
    [InlineData(ToolCaller.Model)]
    public async Task AMessageStartingWithThePrefixIsNeverTyped(ToolCaller caller)
    {
        var input = new RecordingGameInput();
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings = [new EliteBinding("FocusCommsPanel", "Primary", "Keyboard", "Key_Enter")],
        };
        var actions = new ActionSurface
        {
            Binds = () => binds,
            Status = () => new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
            Input = input,
            Enabled = () => true,
        };
        var registry = CapabilityRegistry.Build([CommsCapability.Create(actions, () => true)]);

        var result = await registry.InvokeAsync(
            "send_chat_message",
            new ToolArguments(new Dictionary<string, string>
            {
                ["message"] = "/D47 jump range",
                ["channel"] = "local",
            }),
            TestContext.Current.CancellationToken,
            caller);

        Assert.True(result.IsError);
        Assert.Contains("/d47", result.Content, StringComparison.Ordinal);
        Assert.Empty(input.Steps);
    }
}
