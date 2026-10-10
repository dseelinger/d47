using D47.Core.Configuration;
using D47.Core.Debrief;
using D47.Core.Persona;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Debrief;

/// <summary>A <see cref="DebriefHost"/> over a real settings file and a real standing-directions file.</summary>
[Trait("Category", "Integration")]
public sealed class ADebriefOnDisk : IDisposable
{
    public const string Commander = "F1234";

    /// <summary>A correction the extractor drafts as a direction.</summary>
    public const string Correction = "never mention my rank again";

    public static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();

    public ADebriefOnDisk()
    {
        var store = new SettingsStore(_install.Paths, _install.Files, NullLogger<SettingsStore>.Instance);

        Settings = new SettingsService(
            store,
            new SecretStore(_install.Paths, new ReversibleProtector(), _install.Files, NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        Store = Open();
        Book = new DebriefBook(Store, () => Commander);
        Host = new DebriefHost(Book, () => Now, Settings, new PersonaHost(), NullLogger<DebriefHost>.Instance);
    }

    public SettingsService Settings { get; }

    public StandingDirectionsStore Store { get; }

    public DebriefBook Book { get; }

    public DebriefHost Host { get; }

    /// <summary>A second reader of the same file, as another process would be.</summary>
    public StandingDirectionsStore Open()
    {
        var store = new StandingDirectionsStore(
            Path.Combine(_install.Paths.Data, DebriefWriteFence.FileName),
            _install.Files,
            NullLogger<StandingDirectionsStore>.Instance);

        store.Poll();
        return store;
    }

    public void Switch(bool on) =>
        Settings.Replace("debrief", current => current with { Debrief = current.Debrief with { Enabled = on } });

    /// <summary>One exchange, one line from the game, and enough interruptions to draft a question.</summary>
    public void NoteASession()
    {
        Host.NoteTurn(Correction, "Understood, Commander.");
        Host.NoteHeardFromOutside("Docking request granted.");

        for (var i = 0; i < DebriefExtractor.SignalThreshold; i++)
        {
            Host.NoteInterrupted();
            Host.NoteSilenced(new D47.Core.Callouts.CalloutSilenced("fuel-low", Now, TimeSpan.FromSeconds(2)));
        }
    }

    public void Dispose()
    {
        Host.Dispose();
        _install.Dispose();
    }
}
