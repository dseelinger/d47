using D47.Core.Journal;
using D47.Core.Tests.Persona;

namespace D47.Core.Tests.Stories;

internal static class OdysseySupport
{
    /// <summary>A LoadGame from the fixture corpus, with its Odyssey flag set or, when null, left out.</summary>
    public static JournalEvent LoadGame(DateTimeOffset at, bool? odyssey) => BeaconFixture.Event(
        $$"""{ "timestamp":"{{at.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"F1", "Commander":"Test", "Horizons":true, {{(odyssey is { } flag ? $"\"Odyssey\":{(flag ? "true" : "false")}, " : "")}}"Ship":"SideWinder", "ShipID":1, "GameMode":"Open", "Credits":1000, "Loan":0 }""");
}
