using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

public class AnExistingInstallKeepsItsCoresTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-guardian-cores", Guid.NewGuid().ToString("N"));

    public AnExistingInstallKeepsItsCoresTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private GuardianCores Open(bool installExisted) => GuardianCores.Open(
        Path.Combine(_folder, "guardian-cores.json"), installExisted, NullLogger<GuardianCores>.Instance);

    [Fact]
    public void AnInstallWithSettingsHasEveryCoreAwake()
    {
        var cores = Open(installExisted: true);

        Assert.True(cores.CoresAwake);
        Assert.True(cores.HereticAwake);
        Assert.All(PersonaCatalog.All, persona => Assert.True(cores.IsAwake(persona), persona.Id));
    }

    [Fact]
    public void ANewInstallStartsAsleep()
    {
        var cores = Open(installExisted: false);

        Assert.False(cores.CoresAwake);
        Assert.True(cores.IsAwake(PersonaCatalog.Covas));
    }

    [Fact]
    public void ANewInstallIsNotTakenForAnExistingOneOnItsSecondRun()
    {
        Open(installExisted: false);

        // By its second run a new install has a settings file of its own.
        Assert.False(Open(installExisted: true).CoresAwake);
    }

    [Fact]
    public void AnInheritedInstallIsNotToldTheCoresWoke()
    {
        var cores = Open(installExisted: true);
        var woke = false;
        cores.Woke += _ => woke = true;

        foreach (var journalEvent in BeaconFixture.Events())
        {
            cores.Apply(journalEvent);
        }

        Assert.False(woke);
    }
}
