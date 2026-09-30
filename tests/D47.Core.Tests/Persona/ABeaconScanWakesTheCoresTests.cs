using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Persona.BeaconFixture;

namespace D47.Core.Tests.Persona;

public class ABeaconScanWakesTheCoresTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-guardian-cores", Guid.NewGuid().ToString("N"));

    public ABeaconScanWakesTheCoresTests()
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

    private GuardianCores NewInstall() => GuardianCores.Open(
        Path.Combine(_folder, "guardian-cores.json"), installExisted: false, NullLogger<GuardianCores>.Instance);

    private static void Replay(GuardianCores cores)
    {
        foreach (var journalEvent in Events())
        {
            cores.Apply(journalEvent);
        }
    }

    [Fact]
    public void TheFixtureScanWakesEveryCoreButTheHeretic()
    {
        var cores = NewInstall();
        var woke = new List<CoreWaking>();
        cores.Woke += woke.Add;

        Replay(cores);

        Assert.True(cores.CoresAwake);
        Assert.False(cores.HereticAwake);
        Assert.Equal([CoreWaking.Cores], woke);
        Assert.True(cores.IsAwake(PersonaCatalog.Warden));
        Assert.False(cores.IsAwake(PersonaCatalog.Heretic));
    }

    [Fact]
    public void TheCoresStayAwakeAfterARestart()
    {
        Replay(NewInstall());

        Assert.True(NewInstall().CoresAwake);
    }

    [Fact]
    public void TheLineIsSaidOnceNotOnEveryScan()
    {
        var cores = NewInstall();
        var woke = new List<CoreWaking>();
        cores.Woke += woke.Add;

        Replay(cores);
        cores.Apply(DataPoint());
        cores.Apply(DataPoint());

        Assert.Single(woke);
    }

    [Fact]
    public void ASecondBeaconInAnotherSystemWakesTheHeretic()
    {
        var cores = NewInstall();
        var woke = new List<CoreWaking>();
        cores.Woke += woke.Add;

        Replay(cores);
        cores.Apply(JumpTo("Synuefe IL-N c23-15", 4208161886922));
        cores.Apply(DataPoint());

        Assert.True(cores.HereticAwake);
        Assert.Equal([CoreWaking.Cores, CoreWaking.Heretic], woke);
    }

    [Fact]
    public void UntilThenTheStockCoreSpeaks()
    {
        var host = new PersonaHost(PersonaCatalog.Warden, cores: NewInstall());

        Assert.Same(PersonaCatalog.Covas, host.Current);

        host.Apply(new PersonaSettings { Id = "kex" });

        Assert.Same(PersonaCatalog.Covas, host.Current);
        Assert.DoesNotContain("Guardian", host.RenderBlock(personalityEnabled: true), StringComparison.Ordinal);
    }

    [Fact]
    public void ANewInstallStartsOnTheStockCore()
    {
        Assert.Equal("covas", new PersonaSettings().Id);
        Assert.Same(PersonaCatalog.Covas, PersonaCatalog.Resolve(null));
    }

    [Fact]
    public void ThePickerShowsASleepingCoreLockedAndRefusesIt()
    {
        using var install = new TempInstall();
        var cores = NewInstall();
        var surface = TestSurface.For(install, personas: new PersonaHost(cores: cores));
        var row = surface.Settings.Find(PersonaCapability.PersonaKey)!;

        Assert.Contains("warden", row.ChoicesFor(surface.Settings.Current));
        Assert.Equal("LOCKED", row.StatusFor("warden", surface.Settings.Current)?.Text);
        Assert.Null(row.StatusFor("covas", surface.Settings.Current));

        var refused = surface.Settings.Apply(PersonaCapability.PersonaKey, "warden", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, refused.Status);
        Assert.Contains("locked", refused.Message, StringComparison.Ordinal);
        Assert.Equal("covas", surface.Settings.Current.Persona.Id);

        Replay(cores);

        Assert.Null(row.StatusFor("warden", surface.Settings.Current));
        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(PersonaCapability.PersonaKey, "warden", SettingsCaller.Panel).Status);
        Assert.Equal(
            SettingApplyStatus.Rejected,
            surface.Settings.Apply(PersonaCapability.PersonaKey, "heretic", SettingsCaller.Panel).Status);
    }
}
