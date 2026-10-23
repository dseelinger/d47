using System.Net;
using System.Net.Http;
using D47.App.Usage;
using D47.Core.Capabilities;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class UsageCountsAreSentOncePerCompletedDayTests
{
    private const string CountsFile = @"C:\d47\data\usage-counts.json";

    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AToolCallIsCountedUnderItsToolAndCaller()
    {
        var rig = new Rig();

        await rig.Call("open_tool", ToolCaller.Commander);
        await rig.Call("open_tool", ToolCaller.Model);
        await rig.Call("open_tool", ToolCaller.Model);

        rig.Now = Tuesday.AddDays(1);
        await rig.Reporter.SendDueAsync();

        var body = Assert.Single(rig.Bodies);
        Assert.Contains("\"day\":\"2026-10-20\"", body);
        Assert.Contains("{\"tool\":\"open_tool\",\"caller\":\"commander\",\"count\":1}", body);
        Assert.Contains("{\"tool\":\"open_tool\",\"caller\":\"model\",\"count\":2}", body);
    }

    [Fact]
    public async Task AProtectedToolRefusedToTheModelIsNotCounted()
    {
        var rig = new Rig();

        await rig.Call("locked_tool", ToolCaller.Model);

        rig.Now = Tuesday.AddDays(1);
        await rig.Reporter.SendDueAsync();

        Assert.Empty(rig.Bodies);
    }

    [Fact]
    public async Task WithTheSettingOffNothingIsCountedOrSent()
    {
        var rig = new Rig { Counts = false };

        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);
        await rig.Reporter.SendDueAsync();

        Assert.Empty(rig.Bodies);
    }

    [Fact]
    public async Task TurningTheSettingOffDeletesTheFile()
    {
        var rig = new Rig();
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Reporter.Save();
        Assert.NotNull(rig.Files.Stat(CountsFile));

        rig.Counts = false;
        rig.Reporter.SettingsChanged();

        Assert.Null(rig.Files.Stat(CountsFile));
        Assert.Empty(rig.Reporter.Ledger.CompletedDays(DateOnly.MaxValue));
    }

    [Fact]
    public async Task ACompletedDayIsSentAndRemovedOnA204()
    {
        var rig = new Rig();
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);

        await rig.Reporter.SendDueAsync();
        await rig.Reporter.SendDueAsync();

        Assert.Single(rig.Bodies);
        Assert.Equal("1", rig.Formats.Single());
        Assert.Equal("d47-usage", rig.Agents.Single());
        Assert.EndsWith("/usage", rig.Urls.Single());
        Assert.DoesNotContain("2026-10-20", rig.Files.ReadText(CountsFile) ?? string.Empty);
    }

    [Fact]
    public async Task TheCurrentDayIsNeverSent()
    {
        var rig = new Rig();
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddHours(10);

        await rig.Reporter.SendDueAsync();

        Assert.Empty(rig.Bodies);
    }

    [Fact]
    public async Task A503KeepsTheDay()
    {
        var rig = new Rig { Status = HttpStatusCode.ServiceUnavailable };
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);

        await rig.Reporter.SendDueAsync();
        rig.Status = HttpStatusCode.NoContent;
        await rig.Reporter.SendDueAsync();

        Assert.Equal(2, rig.Bodies.Count);
    }

    [Fact]
    public async Task A400RemovesTheDay()
    {
        var rig = new Rig { Status = HttpStatusCode.BadRequest };
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);

        await rig.Reporter.SendDueAsync();
        await rig.Reporter.SendDueAsync();

        Assert.Single(rig.Bodies);
    }

    [Fact]
    public async Task TwoSendsStartedTogetherMakeOneRequestPerDay()
    {
        var rig = new Rig();
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);

        await Task.WhenAll(rig.Reporter.SendDueAsync(), rig.Reporter.SendDueAsync());

        Assert.Single(rig.Bodies);
    }

    [Fact]
    public async Task ADayOlderThanThirtyDaysIsDroppedNotSent()
    {
        var rig = new Rig();
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(31);

        await rig.Reporter.SendDueAsync();

        Assert.Empty(rig.Bodies);
    }

    [Fact]
    public async Task NoSendRunsOnATestDrive()
    {
        var rig = new Rig(canSend: false);
        await rig.Call("open_tool", ToolCaller.Commander);
        rig.Now = Tuesday.AddDays(1);

        rig.Reporter.Start();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Empty(rig.Bodies);
    }

    private sealed class Rig
    {
        private readonly List<string> _bodies = [];

        public Rig(bool canSend = true)
        {
            Registry = CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "probe",
                    Group = "Foundation",
                    Name = "Probe",
                    Summary = "A capability that exists to be counted by a test.",
                    Tools =
                    [
                        Tool("open_tool", isProtected: false),
                        Tool("locked_tool", isProtected: true),
                    ],
                },
            ]);

            Reporter = new UsageReporter(
                CountsFile,
                Files,
                () => Counts,
                () => Now,
                canSend,
                NullLogger.Instance,
                new Handler(this),
                "https://usage.test");
            Reporter.Follow(Registry);
        }

        public MemoryFileSystem Files { get; } = new();

        public CapabilityRegistry Registry { get; }

        public UsageReporter Reporter { get; }

        public bool Counts { get; set; } = true;

        public DateTimeOffset Now { get; set; } = Tuesday;

        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;

        public IReadOnlyList<string> Bodies => _bodies;

        public List<string> Formats { get; } = [];

        public List<string> Agents { get; } = [];

        public List<string> Urls { get; } = [];

        public Task Call(string tool, ToolCaller caller) =>
            Registry.InvokeAsync(tool, ToolArguments.Empty, TestContext.Current.CancellationToken, caller);

        private static ToolDefinition Tool(string name, bool isProtected) => new()
        {
            Name = name,
            Description = "Does nothing.",
            Protected = isProtected,
            Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
        };

        private sealed class Handler(Rig rig) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                rig._bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                rig.Formats.Add(request.Headers.GetValues("d47-format").Single());
                rig.Agents.Add(request.Headers.UserAgent.ToString());
                rig.Urls.Add(request.RequestUri!.ToString());
                await Task.Delay(20, cancellationToken);

                return new HttpResponseMessage(rig.Status);
            }
        }
    }
}
