using Xunit;

namespace D47.Knowledge.Tests;

public class TheGalNetFeedIsParsedTests
{
    private const string Response = """
        {
          "data": [
            {
              "type": "node--galnet_article",
              "attributes": {
                "title": "Station <b>Reopens</b>",
                "body": { "value": "First paragraph.\r\nSecond <i>paragraph</i> &amp; more.\r\n" },
                "published_at": "2026-10-20T10:30:00+00:00",
                "field_galnet_date": "20 OCT 3312",
                "field_galnet_guid": "abc-123"
              }
            },
            { "attributes": { "title": "No id", "published_at": "2026-10-19T10:30:00+00:00" } }
          ],
          "links": { "next": { "href": "https://cms.zaonce.net/next" } }
        }
        """;

    [Fact]
    public void AnArticleGivesItsFieldsAndParagraphs()
    {
        var page = FrontierGalNetSource.Parse(Response);

        Assert.NotNull(page);
        var article = Assert.Single(page.Articles);
        Assert.Equal("abc-123", article.Id);
        Assert.Equal("Station Reopens", article.Title);
        Assert.Equal(["First paragraph.", "Second paragraph & more."], article.Body);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 10, 30, 0, TimeSpan.Zero), article.Published);
        Assert.Equal("20 OCT 3312", article.GameDate);
        Assert.True(page.HasOlder);
    }

    [Fact]
    public void NoNextLinkMeansNothingOlderFollows()
    {
        var page = FrontierGalNetSource.Parse("""{"data":[],"links":{}}""");

        Assert.NotNull(page);
        Assert.False(page.HasOlder);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"other":1}""")]
    public void AResponseOfTheWrongShapeParsesToNothing(string response)
    {
        Assert.Null(FrontierGalNetSource.Parse(response));
    }
}
