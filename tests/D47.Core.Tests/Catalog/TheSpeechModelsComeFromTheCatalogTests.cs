using D47.Core.Audio;
using D47.Core.Catalog;
using D47.Core.Configuration;

using Xunit;

namespace D47.Core.Tests.Catalog;

[Collection(nameof(SharedModelCatalogCollection))]
public class TheSpeechModelsComeFromTheCatalogTests
{
    [Fact]
    public void ANewElevenLabsDefaultIsWhatNobodyChoosingGets()
    {
        With(SpeechSection.Json(elevenLabsDefault: ElevenLabsModels.V3), () =>
        {
            Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Named(null));
            Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Default);
            Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Named(ElevenLabsModels.Flash));
            Assert.Equal([ElevenLabsModels.V4Turbo, ElevenLabsModels.V3], ElevenLabsModels.All.Select(m => m.Id));
        });

        Assert.Equal(ElevenLabsModels.V4Turbo, ElevenLabsModels.Named(null));
    }

    [Fact]
    public void TheSessionSpendPricesTheSelectedModel()
    {
        With(SpeechSection.Json(v4TurboPrice: "0.04", v3Price: "0.10"), () =>
        {
            var spend = new SpeechSpend();
            spend.Record(TtsProviderCatalog.ElevenLabsId, 20_000);

            Assert.Equal(2.00m, spend.Dollars(On(ElevenLabsModels.V3)));
            Assert.Equal(0.80m, spend.Dollars(On(ElevenLabsModels.V4Turbo)));
            Assert.Equal(0.80m, spend.Dollars(On(null)));
        });
    }

    [Fact]
    public void AModelWithNoPriceTakesTheProvidersListPrice()
    {
        With(SpeechSection.Json(v3Price: "null"), () =>
        {
            var spend = new SpeechSpend();
            spend.Record(TtsProviderCatalog.ElevenLabsId, 20_000);

            Assert.Equal(0.80m, spend.Dollars(On(ElevenLabsModels.V3)));
        });
    }

    private static D47Settings On(string? model) => new()
    {
        Speech = new SpeechSettings { Provider = TtsProviderCatalog.ElevenLabsId, ElevenLabsModel = model },
    };

    private static void With(string speech, Action check)
    {
        var original = ModelCatalogSource.Shared.Current;

        try
        {
            ModelCatalogSource.Shared.Replace(ModelCatalog.Parse($$"""
                {
                  "schema": 1,
                  "published": "2026-10-06",
                  "providers": {},
                  {{speech}}
                }
                """));

            check();
        }
        finally
        {
            ModelCatalogSource.Shared.Replace(original);
        }
    }
}
