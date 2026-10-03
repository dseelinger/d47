using Xunit;

namespace D47.App.Tests;

/// <summary>The composition root keeps a stock core flat: no delivery notes, no humor, no model for its own lines.</summary>
public class CovasSpeaksFlatInTheAppTests
{
    private static readonly string Source = File.ReadAllText(
        Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs"));

    [Fact]
    public void ATurnWithCovasAboardIsNeverToldItMayWriteDeliveryNotes()
    {
        Assert.Contains(
            "Turns.CanBeDirected = () => !Personas.Current.Stock && DirectableIn(VoiceGroup.Aboard);",
            Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCoreHumorDialIsReadWithTheStockCoreAboardFlag()
    {
        Assert.Contains(
            "Humor.DialFor(Settings.Current.Persona, group, Personas.Current.Stock)",
            Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CovasIntroAndReturnAreSaidAsWrittenWithNoModelCall()
    {
        Assert.Contains("change.Current.Stock ? null : await AskAsync(instruction)", Source, StringComparison.Ordinal);
        Assert.Contains("!change.Current.Stock\n", Source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void RewordingIsToldWhetherACoreIsStock()
    {
        Assert.Contains("stockCoreAboard: () => Personas.Current.Stock", Source, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
