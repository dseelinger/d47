using System.Net;
using D47.App.Updates;
using D47.Core;
using D47.Core.Catalog;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class ThePublishedModelCatalogIsFetchedTests
{
    private const string Published = """
        {
          "schema": 1,
          "published": "2099-01-01",
          "providers": {
            "anthropic": {
              "default": "claude-later-6",
              "models": [ { "id": "claude-later-6", "offered": true, "price": { "input": 3, "output": 15 } } ]
            }
          },
          "speech": {
            "elevenlabs": { "default": "e", "models": [ { "id": "e", "offered": true } ] },
            "openai": { "default": "o", "models": [ { "id": "o", "offered": true } ] },
            "cartesia": { "default": "c", "models": [ { "id": "c", "offered": true } ] }
          }
        }
        """;

    private readonly string _root = TempFolders.Create("d47-model-catalog");

    private readonly ModelCatalogSource _source = new(ModelCatalog.Embedded);

    [Fact]
    public async Task AValidCatalogFromThePublishedAddressIsUsed()
    {
        var endpoint = new Endpoint(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Published) });
        using var refresher = Refresher(endpoint, refresh: true);

        await refresher.RefreshAsync(CancellationToken.None);

        Assert.Equal(new Uri(ModelCatalogRefresher.Address), endpoint.Last!.RequestUri);
        Assert.Equal("claude-later-6", _source.Current.DefaultFor("anthropic"));
        Assert.True(File.Exists(Path.Combine(_root, ModelCatalogCache.FileName)));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("timeout")]
    [InlineData("unreachable")]
    public async Task AFailedFetchLeavesTheCatalogInUse(string failure)
    {
        var endpoint = new Endpoint(_ => failure switch
        {
            "error" => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            "timeout" => throw new TaskCanceledException("The request timed out."),
            _ => throw new HttpRequestException("No route to host."),
        });
        using var refresher = Refresher(endpoint, refresh: true);

        await refresher.RefreshAsync(CancellationToken.None);

        Assert.Same(ModelCatalog.Embedded, _source.Current);
        Assert.False(File.Exists(Path.Combine(_root, ModelCatalogCache.FileName)));
    }

    [Fact]
    public async Task WithTheSettingOffNothingIsRequested()
    {
        var endpoint = new Endpoint(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Published) });
        using var refresher = Refresher(endpoint, refresh: false);

        await refresher.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, endpoint.Requests);
        Assert.Same(ModelCatalog.Embedded, _source.Current);
    }

    private ModelCatalogRefresher Refresher(Endpoint endpoint, bool refresh)
    {
        var paths = new AppPaths(_root);
        var store = new SettingsStore(paths, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(paths, new PlainProtector(), NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(
            store,
            secrets,
            new D47Settings { Models = new ModelSettings { RefreshCatalog = refresh } },
            NullLogger<SettingsService>.Instance);
        var cache = new ModelCatalogCache(
            _source,
            ModelCatalog.Embedded,
            Path.Combine(_root, ModelCatalogCache.FileName),
            NullLogger<ModelCatalogCache>.Instance);

        return new ModelCatalogRefresher(cache, settings, NullLogger<ModelCatalogRefresher>.Instance, endpoint);
    }

    private sealed class Endpoint(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;
            Requests++;

            return Task.FromResult(answer(request));
        }
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(byte[] ciphertext, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = ciphertext;
            return true;
        }
    }
}
