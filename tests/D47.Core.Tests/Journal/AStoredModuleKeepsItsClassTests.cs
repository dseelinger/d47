using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A stored module's class is parsed from its symbol, class 1 being E and 5 being A (#563).</summary>
public class AStoredModuleKeepsItsClassTests
{
    private static ModuleStore Store(string items)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""
            {"timestamp":"2026-10-04T10:00:00Z","event":"StoredModules","StarSystem":"Shinrarta Dezhra",
             "StationName":"Jameson Memorial","Items":[{{items}}]}
            """,
            NullLogger.Instance,
            out var parsed));

        return ModuleStore.Empty.Apply(parsed!);
    }

    [Theory]
    [InlineData("$int_cargorack_size5_class1_name;", "5E")]
    [InlineData("$int_hyperdrive_size5_class5_name;", "5A")]
    [InlineData("int_shieldgenerator_size6_class3_fast", "6C")]
    public void TheClassComesFromTheSymbol(string symbol, string expected)
    {
        var module = Assert.Single(Store($$"""{"Name":"{{symbol}}","Name_Localised":"Module"}""").Modules);

        Assert.Equal(expected, module.Class);
    }

    [Theory]
    [InlineData("$int_detailedsurfacescanner_tiny_name;")]
    [InlineData("$hpt_multicannon_gimbal_medium_name;")]
    [InlineData("$int_guardianfsdbooster_size3_name;")]
    public void ASymbolWithNoSizeAndClassHasNoClass(string symbol)
    {
        var module = Assert.Single(Store($$"""{"Name":"{{symbol}}","Name_Localised":"Module"}""").Modules);

        Assert.Null(module.Class);
    }
}
