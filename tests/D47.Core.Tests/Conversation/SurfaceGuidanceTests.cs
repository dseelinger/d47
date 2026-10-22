using System.Globalization;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Marking the Commander's position and giving the bearing back to saved points on the body.</summary>
public class SurfaceGuidanceTests
{
    private const double Radius = 1_000_000;

    private const string Body = "\"SystemAddress\":2175107336563,\"BodyID\":37";

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-08-16T{time}Z", CultureInfo.InvariantCulture);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Line(string time, string kind, string rest) =>
        "{\"timestamp\":\"2026-08-16T" + time + "Z\",\"event\":\"" + kind + "\"," + rest + "}";

    private static string Sample(string time, string scanType, string genus = "Bacterium") =>
        Line(
            time,
            "ScanOrganic",
            $"\"ScanType\":\"{scanType}\",\"Genus\":\"$Codex_Ent_{genus}_Genus_Name;\",\"Genus_Localised\":\"{genus}\","
            + $"\"Species\":\"$Codex_Ent_{genus}_02_Name;\",\"Species_Localised\":\"{genus} Aerial\","
            + "\"SystemAddress\":2175107336563,\"Body\":37");

    private static SurfaceFix Fix(double latitude, double longitude) => new(latitude, longitude, Radius);

    private static string Expected(string subject, SurfaceFix from, SurfaceFix to)
    {
        var metres = from.MetresTo(to);
        var bearing = from.BearingTo(to);

        return $"{subject} is {SurfaceGuidanceCapability.Distance(metres)} away, bearing "
               + $"{(int)Math.Round(bearing) % 360}, {SurfaceGuidanceCapability.CompassPoint(bearing)}.";
    }

    private sealed class Scene
    {
        public CommanderGameState State { get; } = new(new CommanderIdentity("F1", "Fixture"));

        public SurfaceMarks Marks { get; } = new();

        public EarlierFinds Earlier { get; } = new();

        public string StatusJson { get; private set; } = "{}";

        public DateTimeOffset Now { get; set; } = At("10:30:00");

        public Scene Journal(params string[] lines)
        {
            foreach (var line in lines)
            {
                State.Apply(Parse(line), null);
            }

            return this;
        }

        public Scene Landed(string time = "10:00:00") =>
            Journal(Line(time, "Touchdown", Body + ",\"Latitude\":0,\"Longitude\":0"));

        public Scene Standing(double latitude, double longitude, double? heading = null)
        {
            var facing = heading is { } h ? ",\"Heading\":" + Num(h) : string.Empty;

            StatusJson = "{\"Flags\":0,\"Latitude\":" + Num(latitude) + ",\"Longitude\":" + Num(longitude)
                         + ",\"PlanetRadius\":" + Num(Radius) + facing + "}";

            return this;
        }

        public CapabilityRegistry Registry()
        {
            var files = new MemoryFileSystem();
            files.WriteText(Path.Combine(@"C:\j", GameStatusReader.FileName), StatusJson);

            var reader = new GameStatusReader(@"C:\j", files, NullLogger.Instance);
            reader.Poll();

            return CapabilityRegistry.Build(
                [SurfaceGuidanceCapability.Create(Marks, () => State, () => reader.Current, () => Now, Earlier)]);
        }

        public async Task<ToolResult> Run(string tool, string? key = null, string? value = null)
        {
            var arguments = key is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value! };

            return await Registry().InvokeAsync(
                tool, new ToolArguments(arguments), TestContext.Current.CancellationToken);
        }

        public Scene OnBody(string bodyName) =>
            Journal(
                Line(
                    "09:00:00",
                    "Location",
                    "\"StarSystem\":\"Sys\",\"SystemAddress\":2175107336563,\"BodyID\":37,\"Body\":\""
                    + bodyName + "\",\"Docked\":false"));

        public Scene Finds(params EarlierFind[] finds)
        {
            Earlier.Set(2175107336563, finds);
            return this;
        }

        public Task<ToolResult> Mark(string? name = null) =>
            Run(SurfaceGuidanceCapability.MarkTool, name is null ? null : "name", name);

        public Task<ToolResult> Way(string? to = null) =>
            Run(SurfaceGuidanceCapability.BearingTool, to is null ? null : "to", to);
    }

    [Fact]
    public async Task TheShipIsGivenAtTheBearingAndDistanceTheSurfaceMathGives()
    {
        var scene = new Scene().Landed().Standing(-0.01, 0.01);

        var result = await scene.Way();

        Assert.False(result.IsError);
        Assert.Equal(Expected("Your ship", Fix(-0.01, 0.01), Fix(0, 0)), result.Content);
        Assert.Contains("bearing 315, north-west", result.Content);
    }

    [Fact]
    public async Task TheRelativeWordingIsAddedOnlyWhenTheHeadingIsKnown()
    {
        var without = await new Scene().Landed().Standing(-0.01, 0).Way("ship");
        var with = await new Scene().Landed().Standing(-0.01, 0, heading: 90).Way("ship");

        Assert.DoesNotContain("It is", without.Content);
        Assert.EndsWith("north. It is on your left.", with.Content);
    }

    [Theory]
    [InlineData(0, "ahead")]
    [InlineData(22.4, "ahead")]
    [InlineData(22.5, "ahead on your right")]
    [InlineData(67.4, "ahead on your right")]
    [InlineData(67.5, "on your right")]
    [InlineData(112.4, "on your right")]
    [InlineData(112.5, "behind you on your right")]
    [InlineData(157.4, "behind you on your right")]
    [InlineData(157.5, "behind you")]
    [InlineData(202.4, "behind you")]
    [InlineData(202.5, "behind you on your left")]
    [InlineData(247.4, "behind you on your left")]
    [InlineData(247.5, "on your left")]
    [InlineData(292.4, "on your left")]
    [InlineData(292.5, "ahead on your left")]
    [InlineData(337.4, "ahead on your left")]
    [InlineData(337.5, "ahead")]
    [InlineData(359.9, "ahead")]
    public void EverySectorBoundaryFallsOnTheSideTheCommanderExpects(double offset, string expected) =>
        Assert.Equal(expected, SurfaceGuidanceCapability.Relative(offset, 0));

    [Fact]
    public void TheRelativeWordingTurnsWithTheHeadingAcrossNorth()
    {
        Assert.Equal("on your right", SurfaceGuidanceCapability.Relative(10, 280));
        Assert.Equal("on your left", SurfaceGuidanceCapability.Relative(280, 10));
    }

    [Fact]
    public async Task AMarkIsNumberedFromTheLowestNumberNotInUse()
    {
        var scene = new Scene().Landed().Standing(0.001, 0.001);

        Assert.Equal("Marked as mark 1.", (await scene.Mark()).Content);
        Assert.Equal("Marked as mark 2.", (await scene.Mark()).Content);
        Assert.Equal("Marked as crash site.", (await scene.Mark("crash site")).Content);
        Assert.Equal("Marked as mark 3.", (await scene.Mark()).Content);
        Assert.Equal("Marked as mark 4.", (await scene.Mark()).Content);
    }

    [Fact]
    public async Task AMarkNamedLikeANumberedOneKeepsThatNumberTaken()
    {
        var scene = new Scene().Landed().Standing(0.001, 0.001);

        await scene.Mark("mark 1");

        Assert.Equal("Marked as mark 2.", (await scene.Mark()).Content);
    }

    [Fact]
    public async Task NumberingStartsAgainOnANewVisit()
    {
        var scene = new Scene().Landed().Standing(0.001, 0.001);
        await scene.Mark();

        scene.Journal(Line("11:00:00", "Liftoff", "\"Latitude\":0,\"Longitude\":0"));
        scene.Journal(Line("11:05:00", "Touchdown", Body + ",\"Latitude\":0,\"Longitude\":0"));
        scene.Now = At("11:10:00");

        Assert.Equal("Marked as mark 1.", (await scene.Mark()).Content);
    }

    [Fact]
    public async Task MarkingWithNoLivePositionIsRefused()
    {
        var result = await new Scene().Landed().Mark();

        Assert.True(result.IsError);
        Assert.Equal(
            "I have no position for you. Marking a spot needs you on or near the surface.", result.Content);
    }

    [Fact]
    public async Task ANamedMarkIsFoundByItsNameAndIsTheDefaultWithoutAShip()
    {
        var scene = new Scene().Journal(Line("10:00:00", "ApproachBody", Body)).Standing(0.002, 0);
        await scene.Mark("crash site");

        scene.Standing(0, 0);

        var byName = await scene.Way("Crash Site");
        var byDefault = await scene.Way();

        Assert.Equal(Expected("Crash site", Fix(0, 0), Fix(0.002, 0)), byName.Content);
        Assert.Equal(byName.Content, byDefault.Content);
    }

    [Fact]
    public async Task AMarkFromAnEarlierVisitIsIgnored()
    {
        var scene = new Scene().Journal(Line("10:00:00", "ApproachBody", Body)).Standing(0.002, 0);
        await scene.Mark();

        scene.Journal(Line("11:00:00", "ApproachBody", Body));

        var result = await scene.Standing(0, 0).Way();

        Assert.Equal("Nothing is saved on this body to guide you to.", result.Content);
        Assert.Empty(scene.Marks.Valid(scene.State.Surface));
    }

    [Fact]
    public async Task AMarkMadeOnAnotherBodyIsIgnored()
    {
        var scene = new Scene().Journal(Line("10:00:00", "ApproachBody", Body)).Standing(0.002, 0);
        await scene.Mark();

        scene.Journal(
            Line("11:00:00", "LeaveBody", Body),
            Line("11:10:00", "ApproachBody", "\"SystemAddress\":2175107336563,\"BodyID\":38"));

        var result = await scene.Standing(0, 0).Way("mark 1");

        Assert.Equal("Nothing is saved on this body to guide you to.", result.Content);
    }

    [Fact]
    public async Task WithoutAShipTheNewestMarkIsUsedBeforeTheLastSpecimen()
    {
        var scene = new Scene().Journal(Line("10:00:00", "ApproachBody", Body));
        scene.State.Apply(Parse(Sample("10:10:00", "Log")), Fix(0, 0.001));
        scene.State.Apply(Parse(Sample("10:12:00", "Sample")), Fix(0, 0.002));

        var sample = await scene.Standing(0, 0).Way();

        Assert.Equal(Expected("Your last Bacterium sample", Fix(0, 0), Fix(0, 0.002)), sample.Content);

        scene.Now = At("10:20:00");
        await scene.Standing(0.003, 0).Mark();

        var mark = await scene.Standing(0, 0).Way();

        Assert.Equal(Expected("Mark 1", Fix(0, 0), Fix(0.003, 0)), mark.Content);
    }

    [Fact]
    public async Task ASampleIsFoundByGenusOrSpeciesAndByTheWordSample()
    {
        var scene = new Scene().Landed();
        scene.State.Apply(Parse(Sample("10:10:00", "Log")), Fix(0, 0.004));
        scene.Standing(0, 0);

        var expected = Expected("Your last Bacterium sample", Fix(0, 0), Fix(0, 0.004));

        Assert.Equal(expected, (await scene.Way("bacterium")).Content);
        Assert.Equal(expected, (await scene.Way("Bacterium Aerial")).Content);
        Assert.Equal(expected, (await scene.Way("sample")).Content);
    }

    [Fact]
    public async Task NothingSavedIsSaidSo()
    {
        var result = await new Scene().Journal(Line("10:00:00", "ApproachBody", Body)).Standing(0, 0).Way();

        Assert.Equal("Nothing is saved on this body to guide you to.", result.Content);
    }

    [Fact]
    public async Task AnUnknownTargetNamesAtMostThreePointsAndCountsTheRest()
    {
        var scene = new Scene().Landed().Standing(0.001, 0);

        foreach (var name in new[] { "a", "b", "c" })
        {
            await scene.Mark(name);
        }

        var result = await scene.Way("nowhere");

        Assert.Equal(
            "I have nothing called \"nowhere\" on this body. I can guide you to your ship, c, b and 1 more.",
            result.Content);
    }

    [Fact]
    public async Task GuidanceWithNoLivePositionIsRefused()
    {
        var result = await new Scene().Landed().Way();

        Assert.True(result.IsError);
        Assert.StartsWith("I have no position for you.", result.Content);
    }

    [Fact]
    public async Task EachMarkGetsAWhichWayToPhrase()
    {
        var scene = new Scene().Landed().Standing(0.001, 0);
        await scene.Mark("crash site");

        var phrase = Assert.Single(SurfaceGuidanceCapability.Phrases(scene.Marks, () => scene.State));

        Assert.Equal("which way to crash site", phrase.Phrase);
        Assert.Equal("crash site", phrase.Arguments["to"]);
    }

    [Fact]
    public void NeitherToolIsProtected()
    {
        var tools = new Scene().Registry().Find(SurfaceGuidanceCapability.Id)!.Descriptor.Tools;

        Assert.Equal(2, tools.Count);
        Assert.All(tools, tool => Assert.False(tool.Protected));
    }

    private static EarlierFind Found(string body, string name, double latitude, double longitude) =>
        new(body, 1, name, latitude, longitude);

    [Fact]
    public async Task TheNearestEarlierFindIsGivenAtTheBearingAndDistanceTheSurfaceMathGives()
    {
        var scene = new Scene().Landed().OnBody("Sys 1 a").Standing(0, 0.01).Finds(
            Found("Sys 1 a", "Bacterium Aurasus - Green", -0.02, 0.01),
            Found("Sys 1 a", "Bacterium Aurasus - Green", 0.005, 0.01));

        var result = await scene.Way("nearest Aurasus");

        Assert.False(result.IsError);
        Assert.Equal(
            Expected("The nearest earlier Bacterium Aurasus - Green find", Fix(0, 0.01), Fix(0.005, 0.01)),
            result.Content);
    }

    [Fact]
    public async Task NearestWithNoNameUsesAnyVariantAndAddsTheRelativeWording()
    {
        var scene = new Scene().Landed().OnBody("Sys 1 a").Standing(0, 0, heading: 90).Finds(
            Found("Sys 1 a", "Fonticulua Campestris", 0, 0.02),
            Found("Sys 1 a", "Bacterium Aurasus - Green", 0, 0.01));

        var result = await scene.Way("nearest");

        Assert.StartsWith("The nearest earlier Bacterium Aurasus - Green find is", result.Content);
        Assert.EndsWith("bearing 90, east. It is ahead.", result.Content);
    }

    [Fact]
    public async Task AFindOnAnotherBodyIsNeverUsed()
    {
        var scene = new Scene().Landed().OnBody("Sys 1 a").Standing(0, 0).Finds(
            Found("Sys 1 b", "Bacterium Aurasus - Green", 0, 0.001));

        var result = await scene.Way("nearest Bacterium");

        Assert.Equal("No Commander has logged an earlier Bacterium find on this body.", result.Content);
    }

    [Fact]
    public async Task NearestWithNothingMatchingSaysSo()
    {
        var scene = new Scene().Landed().OnBody("Sys 1 a").Standing(0, 0).Finds(
            Found("Sys 1 a", "Fonticulua Campestris", 0, 0.001));

        Assert.Equal(
            "No Commander has logged an earlier Bacterium find on this body.", (await scene.Way("nearest Bacterium")).Content);
        Assert.Equal("No Commander has logged an earlier find on this body.", (await new Scene().Landed().Standing(0, 0).Way("nearest")).Content);
    }

    [Fact]
    public async Task APlainGenusStillMeansTheCommandersOwnSpecimen()
    {
        var scene = new Scene().Landed().OnBody("Sys 1 a").Standing(0, 0).Finds(
            Found("Sys 1 a", "Bacterium Aurasus - Green", 0, 0.001));
        scene.State.Apply(Parse(Sample("10:10:00", "Log")), Fix(0, 0.004));

        var result = await scene.Way("Bacterium");

        Assert.StartsWith("Your last Bacterium sample is", result.Content);
    }

    [Fact]
    public void TheNearestEarlierFindHasModelFreePhrases()
    {
        var tool = new Scene().Registry().Find(SurfaceGuidanceCapability.Id)!.Descriptor.Tools
            .Single(candidate => candidate.Name == SurfaceGuidanceCapability.BearingTool);

        Assert.Contains(tool.Commands, command => command.Phrase == "which way to the nearest earlier find");
        Assert.Contains(tool.Commands, command => command.Phrase == "guide me to the nearest earlier find");
    }
}
