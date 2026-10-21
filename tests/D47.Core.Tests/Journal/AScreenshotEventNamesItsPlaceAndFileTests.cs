using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AScreenshotEventNamesItsPlaceAndFileTests
{
    private static readonly JournalLocation NoPlace = JournalLocation.Unknown;

    private static JournalEvent Event(string fields)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""{ "timestamp":"2026-02-10T09:05:00Z", "event":"Screenshot", {{fields}} }""", NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EliteScreenshot Shot(string fields, JournalLocation? fallback = null) =>
        Assert.IsType<EliteScreenshot>(EliteScreenshot.TryParse(Event(fields), fallback ?? NoPlace));

    private static string Picture(string name) => $$"""\\ED_Pictures\\{{name}}""";

    [Fact]
    public void AnEventWithASystemAndBodyKeepsThem()
    {
        var shot = Shot($$"""{{Fields("A.bmp")}}, "System":"Example Sector AB-C d1-23", "Body":"Example Sector AB-C d1-23 A 1" """);

        Assert.Equal("A.bmp", shot.FileName);
        Assert.Equal(1920, shot.Width);
        Assert.Equal(1080, shot.Height);
        Assert.Equal("Example Sector AB-C d1-23", shot.System);
        Assert.Equal("A 1", shot.Body);
    }

    [Fact]
    public void AnEventWithoutASystemTakesTheFallbackPlace()
    {
        var shot = Shot(Fields("A.bmp"), new JournalLocation("Example", "Example 2", false, null));

        Assert.Equal("Example", shot.System);
        Assert.Equal("2", shot.Body);
    }

    [Fact]
    public void AnEventAndAFallbackWithNoPlaceHaveNone()
    {
        var shot = Shot(Fields("A.bmp"));

        Assert.Null(shot.System);
        Assert.Null(shot.Body);
        Assert.Equal("2026-02-10 09-05-00.jpg", shot.Name(new DateTime(2026, 2, 10, 9, 5, 0), 1));
    }

    [Theory]
    [InlineData("""\\Other\\A.bmp""")]
    [InlineData("""\\ED_Pictures\\..\\A.bmp""")]
    [InlineData("""\\ED_Pictures\\Sub\\A.bmp""")]
    [InlineData("""\\ED_Pictures\\A.jpg""")]
    [InlineData("""\\ED_Pictures\\A:B.bmp""")]
    [InlineData("""\\ED_Pictures\\.bmp""")]
    public void AFileOutsideThePicturesFolderIsRefused(string filename)
    {
        Assert.Null(EliteScreenshot.TryParse(Event($"\"Filename\":\"{filename}\""), NoPlace));
    }

    [Fact]
    public void AnotherEventKindIsRefused()
    {
        Assert.True(JournalEvent.TryParse("""{ "timestamp":"2026-02-10T09:05:00Z", "event":"Music", "Filename":"\\ED_Pictures\\A.bmp" }""", NullLogger.Instance, out var other));
        Assert.Null(EliteScreenshot.TryParse(other!, NoPlace));
    }

    [Theory]
    [InlineData("Example", "Example", 1, "2026-02-10 09-05-00 Example.jpg")]
    [InlineData("Example", "Example 2", 2, "2026-02-10 09-05-00 Example 2 (2).jpg")]
    [InlineData("Exa:mple", "Exa:mple 3", 3, "2026-02-10 09-05-00 Example 3 (3).jpg")]
    public void TheJpegNameIsTimeSystemAndBody(string system, string body, int copy, string expected)
    {
        var shot = new EliteScreenshot("A.bmp", 1, 1, system, EliteScreenshot.ShortBody(system, body));

        Assert.Equal(expected, shot.Name(new DateTime(2026, 2, 10, 9, 5, 0), copy));
    }

    [Fact]
    public void ABodyLosesItsSystemPrefixInTheName()
    {
        var shot = Shot($$"""{{Fields("A.bmp")}}, "System":"Example Sector AB-C d1-23", "Body":"Example Sector AB-C d1-23 A 1" """);

        Assert.Equal("2026-02-10 09-05-00 Example Sector AB-C d1-23 A 1.jpg", shot.Name(new DateTime(2026, 2, 10, 9, 5, 0), 1));
    }

    [Fact]
    public void TheSourcePathJoinsTheFolderAndTheFile()
    {
        Assert.Equal(Path.Combine("pics", "A.bmp"), Shot(Fields("A.bmp")).SourcePath("pics"));
    }

    [Fact]
    public void TheExpectedLengthIsTheHeadersBfSize()
    {
        using var stream = new MemoryStream([(byte)'B', (byte)'M', 0x78, 0x56, 0x34, 0x12, 0, 0]);

        Assert.Equal(0x12345678L, EliteScreenshot.ExpectedLength(stream));
    }

    [Fact]
    public void AShortStreamHasNoExpectedLength()
    {
        using var stream = new MemoryStream([(byte)'B', (byte)'M', 1, 2, 3]);

        Assert.Null(EliteScreenshot.ExpectedLength(stream));
    }

    [Fact]
    public void AStreamNotStartingWithBmHasNoExpectedLength()
    {
        using var stream = new MemoryStream([(byte)'P', (byte)'N', 1, 2, 3, 4]);

        Assert.Null(EliteScreenshot.ExpectedLength(stream));
    }

    [Theory]
    [InlineData("""{ "Filename":"x", "System":"Example", "Body":"Example 2" }""", "Screenshot taken at 2, Example")]
    [InlineData("""{ "Filename":"x", "System":"Example", "Body":"Example" }""", "Screenshot taken in Example")]
    [InlineData("""{ "Filename":"x", "System":"Example" }""", "Screenshot taken in Example")]
    [InlineData("""{ "Filename":"x" }""", "Screenshot taken")]
    public void TheSentenceNamesThePlaceItHas(string fields, string expected)
    {
        Assert.Equal(expected, JournalSentence.For(Event(fields[1..^1].Trim())));
    }

    private static string Fields(string name) =>
        $"\"Filename\":\"{Picture(name)}\", \"Width\":1920, \"Height\":1080";
}
