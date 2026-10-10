using D47.Core.Catalog;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

using Xunit;

namespace D47.Core.Tests.Catalog;

[Collection(nameof(SharedModelCatalogCollection))]
public sealed class TheNewestValidCatalogIsTheOneInUseTests
{
    private readonly MemoryInstall _install = new();

    private readonly RecordingLogger<ModelCatalogCache> _logger = new();

    private string CachePath => Path.Combine(_install.Root, ModelCatalogCache.FileName);

    [Fact]
    public void AFetchedCatalogWithANewDefaultIsUsedWithoutARestart()
    {
        var original = ModelCatalogSource.Shared.Current;

        try
        {
            var cache = new ModelCatalogCache(ModelCatalogSource.Shared, ModelCatalog.Embedded, _install.Files, CachePath, _logger);

            Assert.True(cache.Offer(Catalog("2099-01-01", "claude-later-6")));

            Assert.Equal("claude-later-6", LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).DefaultModel);
            Assert.Equal(Catalog("2099-01-01", "claude-later-6"), _install.Files.ReadText(CachePath));
        }
        finally
        {
            ModelCatalogSource.Shared.Replace(original);
        }
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("schema 2")]
    [InlineData("default not offered")]
    public void AFetchedCatalogThatDoesNotReadIsNeitherUsedNorKeptAndIsLoggedOnce(string fault)
    {
        var json = fault switch
        {
            "schema 2" => Catalog("2099-01-01", "model-a", schema: 2),
            "default not offered" => Catalog("2099-01-01", "model-z"),
            _ => fault,
        };
        var embedded = ModelCatalog.Parse(Catalog("2026-09-28", "model-a"));
        var source = new ModelCatalogSource(embedded);
        var cache = new ModelCatalogCache(source, embedded, _install.Files, CachePath, _logger);

        Assert.False(cache.Offer(json));
        Assert.False(cache.Offer(json));

        Assert.Same(embedded, source.Current);
        Assert.Null(_install.Files.Stat(CachePath));
        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void AtStartupACachedCatalogNewerThanTheEmbeddedOneIsUsed()
    {
        _install.Files.WriteText(CachePath, Catalog("2026-10-01", "model-b"));
        var embedded = ModelCatalog.Parse(Catalog("2026-09-28", "model-a"));
        var source = new ModelCatalogSource(embedded);

        new ModelCatalogCache(source, embedded, _install.Files, CachePath, _logger).Load();

        Assert.Equal("model-b", source.Current.DefaultFor("anthropic"));
    }

    [Fact]
    public void AtStartupAReleaseWhoseCatalogIsNewerThanTheCacheWins()
    {
        _install.Files.WriteText(CachePath, Catalog("2026-09-28", "model-b"));
        var embedded = ModelCatalog.Parse(Catalog("2026-10-01", "model-a"));
        var source = new ModelCatalogSource(embedded);

        new ModelCatalogCache(source, embedded, _install.Files, CachePath, _logger).Load();

        Assert.Same(embedded, source.Current);
    }

    [Fact]
    public void AnUnreadableCacheLeavesTheEmbeddedCatalogInUse()
    {
        _install.Files.WriteText(CachePath, "{ not json");
        var embedded = ModelCatalog.Parse(Catalog("2026-09-28", "model-a"));
        var source = new ModelCatalogSource(embedded);

        new ModelCatalogCache(source, embedded, _install.Files, CachePath, _logger).Load();

        Assert.Same(embedded, source.Current);
    }

    [Fact]
    public void AnOlderFetchedCatalogReplacesNeitherTheOneInUseNorTheCache()
    {
        var newer = Catalog("2026-10-01", "model-b");
        _install.Files.WriteText(CachePath, newer);
        var embedded = ModelCatalog.Parse(Catalog("2026-09-01", "model-a"));
        var source = new ModelCatalogSource(embedded);
        var cache = new ModelCatalogCache(source, embedded, _install.Files, CachePath, _logger);
        cache.Load();

        Assert.True(cache.Offer(Catalog("2026-09-15", "model-a")));

        Assert.Equal("model-b", source.Current.DefaultFor("anthropic"));
        Assert.Equal(newer, _install.Files.ReadText(CachePath));
    }

    private static string Catalog(string published, string anthropicDefault, int schema = 1) => $$"""
        {
          "schema": {{schema}},
          "published": "{{published}}",
          "providers": {
            "anthropic": {
              "default": "{{anthropicDefault}}",
              "backgroundDefault": null,
              "models": [
                { "id": "{{anthropicDefault}}", "offered": {{(anthropicDefault == "model-z" ? "false" : "true")}}, "price": { "input": 3, "output": 15 } },
                { "id": "model-filler", "offered": true, "price": { "input": 3, "output": 15 } }
              ]
            }
          },
          {{SpeechSection.Json()}}
        }
        """;
}
