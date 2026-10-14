using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>While the newest journal names nobody, d47 shows the last Commander an older one logged in as.</summary>
public class StartOnTheLastCommanderWhoLoggedInTests
{
    private const string Root = @"C:\d47-test\journals";

    private const string Header = """{ "timestamp":"2026-01-02T00:00:00Z", "event":"Fileheader", "part":1, "gameversion":"4.2" }""";

    private readonly MemoryFileSystem _files = new();

    private readonly GameStateStore _store = new();

    private readonly List<CommanderSwitch> _raised = [];

    private JournalSpine Spine()
    {
        _store.CommanderChanged += _raised.Add;
        return new JournalSpine(Root, _files, _store, NullLoggerFactory.Instance);
    }

    private void Write(string stamp, params string[] lines) =>
        _files.WriteText(Path.Combine(Root, $"Journal.{stamp}.01.log"), string.Join("\n", lines) + "\n");

    private void Append(string stamp, string line) =>
        _files.AppendText(Path.Combine(Root, $"Journal.{stamp}.01.log"), line + "\n");

    private static string Commander(string fid, string name) =>
        $$"""{ "timestamp":"2026-01-01T00:00:01Z", "event":"Commander", "FID":"{{fid}}", "Name":"{{name}}" }""";

    private static string LoadGame(string fid, string name) =>
        $$"""{ "timestamp":"2026-01-02T00:01:00Z", "event":"LoadGame", "FID":"{{fid}}", "Commander":"{{name}}" }""";

    private void AliceLoggedInLastTime()
    {
        Write("2026-01-01T000000", Header, Commander("F1", "Alice"), LoadGame("F1", "Alice"));
        Write("2026-01-02T000000", Header);
    }

    [Fact]
    public void ANewestFileThatNamesNobodyStartsOnTheLastCommander()
    {
        AliceLoggedInLastTime();

        Spine().Poll(priming: true);

        Assert.Equal("Alice", _store.Active!.Identity.Name);
        Assert.Null(_store.InGame);
        var change = Assert.Single(_raised);
        Assert.True(change.IsAdoption);
        Assert.True(change.Priming);
    }

    [Fact]
    public void TheSeededCommanderLoggingInChangesNothingButWhoIsInTheGame()
    {
        AliceLoggedInLastTime();
        var spine = Spine();
        spine.Poll(priming: true);

        Append("2026-01-02T000000", LoadGame("F1", "Alice"));
        spine.Poll();

        Assert.Equal("Alice", _store.Active!.Identity.Name);
        Assert.Equal("Alice", _store.InGame!.Identity.Name);
        Assert.Single(_raised);
    }

    [Fact]
    public void AnotherCommanderLoggingInIsShownInPlaceOfTheSeed()
    {
        AliceLoggedInLastTime();
        var spine = Spine();
        spine.Poll(priming: true);

        Append("2026-01-02T000000", LoadGame("F2", "Bob"));
        spine.Poll();

        Assert.Equal("Bob", _store.Active!.Identity.Name);
        Assert.Equal("Bob", _store.InGame!.Identity.Name);
        Assert.Equal(2, _raised.Count);
        Assert.Equal("Alice", _raised[1].Previous!.Name);
        Assert.False(_raised[1].Priming);
    }

    [Fact]
    public void NoCommanderInAnyFileLeavesNobodyShown()
    {
        Write("2026-01-01T000000", Header);
        Write("2026-01-02T000000", Header);

        Spine().Poll(priming: true);

        Assert.Null(_store.Active);
        Assert.Empty(_raised);
    }

    [Fact]
    public void ANewestFileThatNamesSomebodyIsNotSeeded()
    {
        Write("2026-01-01T000000", Header, Commander("F1", "Alice"));
        Write("2026-01-02T000000", Header, Commander("F2", "Bob"));

        Spine().Poll(priming: true);

        Assert.Equal("Bob", _store.Active!.Identity.Name);
        Assert.Equal("Bob", Assert.Single(_raised).Current.Name);
        Assert.Single(_store.All);
    }

    [Fact]
    public void TheNewestOlderFileToNameSomebodyIsTheSeed()
    {
        Write("2026-01-01T000000", Header, Commander("F1", "Alice"));
        Write("2026-01-01T120000", Header, Commander("F3", "Carol"));
        Write("2026-01-01T180000", Header);
        Write("2026-01-02T000000", Header);

        Spine().Poll(priming: true);

        Assert.Equal("Carol", _store.Active!.Identity.Name);
    }

    [Fact]
    public void TheSeedIsNotSoughtAgainOnLaterPolls()
    {
        Write("2026-01-02T000000", Header);
        var spine = Spine();
        spine.Poll(priming: true);

        Write("2026-01-01T000000", Header, Commander("F1", "Alice"));
        spine.Poll();

        Assert.Null(_store.Active);
    }
}
