using D47.Core.Catalog;

using Xunit;

namespace D47.Core.Tests.Catalog;

public class AModelCatalogItCannotUseIsRefusedTests
{
    [Fact]
    public void TheBuiltInCatalogIsReadable()
    {
        var catalog = ModelCatalog.Embedded;

        Assert.Equal("claude-sonnet-5-5", catalog.DefaultFor("anthropic"));
        Assert.Equal("gpt-5.6-terra", catalog.DefaultFor("openai"));
        Assert.NotEmpty(catalog.OfferedFor("anthropic"));
        Assert.NotEmpty(catalog.OfferedFor("openai"));
    }

    [Fact]
    public void ACatalogThatParsesGivesItsFactsBack()
    {
        var catalog = ModelCatalog.Parse(Catalog());

        Assert.Equal(new DateOnly(2026, 9, 28), catalog.Published);
        Assert.Equal("model-a", catalog.DefaultFor("anthropic"));
        Assert.Equal(["model-a", "model-b"], catalog.OfferedFor("anthropic"));
        Assert.Equal(4m, catalog.PriceFor("anthropic", "model-b")!.InputPerMillion);
        Assert.Equal(0.05m, catalog.PriceFor("anthropic", "model-b")!.CacheReadFactor);
        Assert.Null(catalog.PriceFor("anthropic", "model-c"));
        Assert.True(catalog.TraitsFor("anthropic", "model-c").ToolSearch);
        Assert.Equal(512, catalog.TraitsFor("anthropic", "model-c").MinimumCacheablePrefix);
    }

    [Fact]
    public void AModelItDoesNotDescribeIsUnknown()
    {
        var catalog = ModelCatalog.Parse(Catalog());

        Assert.Null(catalog.PriceFor("anthropic", "model-z"));
        Assert.Same(ModelTraits.Unknown, catalog.TraitsFor("anthropic", "model-z"));
        Assert.Null(catalog.DefaultFor("openaiCompatible"));
        Assert.Empty(catalog.OfferedFor("openaiCompatible"));
    }

    [Fact]
    public void AnUnknownSchemaIsRefused()
    {
        Assert.Throws<FormatException>(() => ModelCatalog.Parse(Catalog(schema: 2)));
    }

    [Fact]
    public void ADefaultThatIsNotOfferedIsRefused()
    {
        Assert.Throws<FormatException>(() => ModelCatalog.Parse(Catalog(defaultModel: "model-c")));
    }

    [Fact]
    public void AnOfferedModelWithNoPriceIsRefused()
    {
        Assert.Throws<FormatException>(() => ModelCatalog.Parse(Catalog(unpricedOffered: true)));
    }

    [Fact]
    public void ADuplicateIdIsRefused()
    {
        Assert.Throws<FormatException>(() => ModelCatalog.Parse(Catalog(duplicate: true)));
    }

    [Fact]
    public void AFileThatIsNotACatalogIsRefused()
    {
        Assert.Throws<FormatException>(() => ModelCatalog.Parse("""{ "schema": 1 }"""));
        Assert.Throws<FormatException>(() => ModelCatalog.Parse("not json"));
    }

    private static string Catalog(
        int schema = 1,
        string defaultModel = "model-a",
        bool unpricedOffered = false,
        bool duplicate = false)
    {
        var extra = duplicate
            ? """, { "id": "model-a", "offered": true, "price": { "input": 1, "output": 5 } }"""
            : string.Empty;

        return $$"""
            {
              "schema": {{schema}},
              "published": "2026-09-28",
              "providers": {
                "anthropic": {
                  "default": "{{defaultModel}}",
                  "backgroundDefault": null,
                  "models": [
                    { "id": "model-a", "offered": true, "price": { "input": 2, "output": 10, "cacheRead": 0.1, "cacheWrite": 1.25 } },
                    { "id": "model-b", "offered": true, "price": {{(unpricedOffered ? "null" : """{ "input": 4, "output": 20, "cacheRead": 0.05, "cacheWrite": 1.25 }""")}} },
                    { "id": "model-c", "offered": false, "price": null, "traits": { "toolSearch": true, "minimumCacheablePrefix": 512 } }{{extra}}
                  ]
                }
              }
            }
            """;
    }
}
