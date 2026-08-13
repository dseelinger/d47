using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests;

public class CapabilityRegistryTests
{
    private static CapabilityDescriptor Descriptor(
        string id = "example",
        string toolName = "do_thing",
        IReadOnlyList<ToolParameter>? parameters = null,
        ToolHandler? handler = null) =>
        new()
        {
            Id = id,
            Group = "Test",
            Name = "Example",
            Summary = "An example capability.",
            Tools =
            [
                new ToolDefinition
                {
                    Name = toolName,
                    Description = "Does a thing.",
                    Parameters = parameters ?? [],
                    Handler = handler ?? ((_, _) => Task.FromResult(ToolResult.Ok("done"))),
                },
            ],
        };

    [Fact]
    public async Task ARequestProducesARealToolCallThatReturnsAResult()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);

        var result = await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.False(result.IsError);
        Assert.Equal("done", result.Content);
    }

    [Fact]
    public async Task UnknownToolIsAnErrorResultNotAnException()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);

        var result = await registry.Invoke("nonexistent", ToolArguments.Empty);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task MissingRequiredArgumentIsRejectedBeforeTheHandlerRuns()
    {
        var handlerRan = false;
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(
                parameters:
                [
                    new ToolParameter
                    {
                        Name = "target",
                        Type = ToolParameterType.String,
                        Description = "What to do it to.",
                        Required = true,
                    },
                ],
                handler: (_, _) =>
                {
                    handlerRan = true;
                    return Task.FromResult(ToolResult.Ok("done"));
                }),
        ]);

        var result = await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.True(result.IsError);
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task ValueOutsideAClosedVocabularyNeverReachesCapabilityCode()
    {
        var handlerRan = false;
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(
                parameters:
                [
                    new ToolParameter
                    {
                        Name = "mode",
                        Type = ToolParameterType.String,
                        Description = "Which mode.",
                        Required = true,
                        AllowedValues = ["docked", "supercruise"],
                    },
                ],
                handler: (_, _) =>
                {
                    handlerRan = true;
                    return Task.FromResult(ToolResult.Ok("done"));
                }),
        ]);

        var result = await registry.Invoke(
            "do_thing",
            new ToolArguments(new Dictionary<string, string> { ["mode"] = "hyperspace" }));

        Assert.True(result.IsError);
        Assert.False(handlerRan);
        Assert.Contains("supercruise", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventedParameterIsRejected()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);

        var result = await registry.Invoke(
            "do_thing",
            new ToolArguments(new Dictionary<string, string> { ["imaginary"] = "yes" }));

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task AHandlerThatThrowsBecomesAnErrorResult()
    {
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(handler: (_, _) => throw new InvalidOperationException("the scoop is jammed")),
        ]);

        var result = await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.True(result.IsError);
        Assert.Contains("the scoop is jammed", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The announcement is what the coverage record is built from, and "it ran" and "it ran and
    /// worked" are different answers. Announcing on entry, as this used to, could only ever
    /// give the first one.
    /// </summary>
    [Fact]
    public async Task AToolThatWorkedIsAnnouncedAsSucceeded()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);
        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.Equal([new ToolInvocation("do_thing", true)], announced);
    }

    [Fact]
    public async Task AHandlerThatThrewIsAnnouncedAsFailed()
    {
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(handler: (_, _) => throw new InvalidOperationException("the scoop is jammed")),
        ]);

        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.Equal([new ToolInvocation("do_thing", false)], announced);
    }

    /// <summary>
    /// A handler is allowed to report failure without throwing, and that reads the same to the
    /// model — so it has to read the same here.
    /// </summary>
    [Fact]
    public async Task AHandlerThatReturnedAnErrorIsAnnouncedAsFailed()
    {
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(handler: (_, _) => Task.FromResult(ToolResult.Error("no journal yet"))),
        ]);

        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.Equal([new ToolInvocation("do_thing", false)], announced);
    }

    /// <summary>
    /// The distinction the whole outcome rests on. A hallucinated parameter is the registry's
    /// guard holding, not the capability breaking — the same reading as a rejected settings
    /// value — so it must not paint the tool as failing.
    /// </summary>
    [Theory]
    [InlineData("imaginary")]
    [InlineData("choice")]
    public async Task ARefusedArgumentIsAnnouncedAsSucceededBecauseTheGuardHeld(string parameter)
    {
        var registry = CapabilityRegistry.Build(
        [
            Descriptor(parameters:
            [
                new ToolParameter
                {
                    Name = "choice",
                    Type = ToolParameterType.String,
                    Description = "One of a closed set.",
                    AllowedValues = ["one", "two"],
                },
            ]),
        ]);

        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        var result = await registry.Invoke(
            "do_thing",
            new ToolArguments(new Dictionary<string, string> { [parameter] = "three" }));

        Assert.True(result.IsError);
        Assert.Equal([new ToolInvocation("do_thing", true)], announced);
    }

    /// <summary>Nothing in the inventory to attribute it to, so nothing is claimed about it.</summary>
    [Fact]
    public async Task AnUnknownToolIsNotAnnouncedAtAll()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);
        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await registry.Invoke("nonexistent", ToolArguments.Empty);

        Assert.Empty(announced);
    }

    /// <summary>
    /// An interrupted call is not a verdict on the tool. Recording it either way would be a
    /// claim the run never made.
    /// </summary>
    [Fact]
    public async Task ACancelledCallAnnouncesNothing()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var registry = CapabilityRegistry.Build(
        [
            Descriptor(handler: (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(ToolResult.Ok("done"));
            }),
        ]);

        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => registry.InvokeAsync("do_thing", ToolArguments.Empty, cancelled.Token));

        Assert.Empty(announced);
    }

    /// <summary>
    /// Once per call, not once per return path. A tool announced twice would be a tool whose
    /// second announcement could overwrite the first outcome with the wrong one.
    /// </summary>
    [Fact]
    public async Task AToolIsAnnouncedExactlyOncePerCall()
    {
        var registry = CapabilityRegistry.Build([Descriptor()]);
        var announced = new List<ToolInvocation>();
        registry.ToolInvoked += announced.Add;

        await registry.Invoke("do_thing", ToolArguments.Empty);
        await registry.Invoke("do_thing", ToolArguments.Empty);

        Assert.Equal(2, announced.Count);
    }

    [Fact]
    public void DuplicateCapabilityIdFailsAtStartup()
    {
        Assert.Throws<CapabilityRegistrationException>(() =>
            CapabilityRegistry.Build([Descriptor(toolName: "one"), Descriptor(toolName: "two")]));
    }

    [Fact]
    public void DuplicateToolNameFailsAtStartup()
    {
        Assert.Throws<CapabilityRegistrationException>(() =>
            CapabilityRegistry.Build([Descriptor(id: "first"), Descriptor(id: "second")]));
    }

    [Theory]
    [InlineData("Not_Kebab")]
    [InlineData("trailing-")]
    [InlineData("has spaces")]
    public void CapabilityIdMustBeUsableAsAFilenameAndUrl(string id)
    {
        Assert.Throws<CapabilityRegistrationException>(() => CapabilityRegistry.Build([Descriptor(id: id)]));
    }

    [Fact]
    public void ToolNameMustBeSnakeCase()
    {
        Assert.Throws<CapabilityRegistrationException>(() =>
            CapabilityRegistry.Build([Descriptor(toolName: "DoThing")]));
    }
}

public class DiagnosticsCapabilityTests
{
    // The whole surface, not the one capability: the verbosity tool writes a settings row, so
    // the row table and the level switches both have to be wired the way the app wires them.
    private static (CapabilityRegistry Registry, FakeVerbosityControl Verbosity) Build(TempInstall install)
    {
        var surface = TestSurface.For(install);

        return (surface.Registry, surface.Verbosity);
    }

    [Fact]
    public async Task StatusReportsVersionAndWhereWritableFilesLive()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install);

        var result = await registry.Invoke("get_app_status", ToolArguments.Empty);

        Assert.False(result.IsError);
        Assert.Contains(TestSurface.Version, result.Content, StringComparison.Ordinal);
        Assert.Contains(install.Paths.Data, result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerbosityChangesTakeEffectWithNoRestart()
    {
        using var install = new TempInstall();
        var (registry, verbosity) = Build(install);

        Assert.Equal(LogLevel.Information, verbosity.Levels["Journal"]);

        var result = await registry.Invoke(
            "set_log_verbosity",
            new ToolArguments(new Dictionary<string, string> { ["subsystem"] = "journal", ["level"] = "Trace" }));

        Assert.False(result.IsError);
        Assert.Equal(LogLevel.Trace, verbosity.Levels["Journal"]);
    }

    [Fact]
    public async Task AnInventedSubsystemIsRefused()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install);

        var result = await registry.Invoke(
            "set_log_verbosity",
            new ToolArguments(new Dictionary<string, string> { ["subsystem"] = "Telepathy", ["level"] = "Trace" }));

        Assert.True(result.IsError);
    }

    [Fact]
    public void SettingsRowsAreProjectedFromTheSameClosedSetTheToolUses()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install);

        var capability = registry.Find(DiagnosticsCapability.Id);
        Assert.NotNull(capability);

        // One row per subsystem plus the default. If a subsystem is added, both the tool's
        // vocabulary and the settings rows follow from the same list.
        Assert.Equal(
            D47.Core.Diagnostics.Subsystems.All.Count + 1,
            capability.Descriptor.Settings.Count);
    }
}

/// <summary>
/// Passes the test's cancellation token, which xUnit's analyzer asks for and which makes a
/// hung capability fail the run rather than stall it.
/// </summary>
internal static class RegistryTestExtensions
{
    public static Task<ToolResult> Invoke(
        this CapabilityRegistry registry,
        string toolName,
        ToolArguments arguments) =>
        registry.InvokeAsync(toolName, arguments, TestContext.Current.CancellationToken);
}
