using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A secret row declared per Commander is stored under that Commander's name for it.</summary>
public class ACommandersKeyIsTheirOwnTests
{
    private const string Mine = "test.commanderKey";
    private const string Shared = "test.installationKey";

    private readonly MemoryInstall _install = new();

    private SecretStore Secrets() =>
        new(_install.Paths, new ReversibleProtector(), _install.Files, NullLogger<SecretStore>.Instance);

    private (SettingsService Settings, SecretStore Secrets) Service()
    {
        var secrets = Secrets();
        var settings = new SettingsService(
            new SettingsStore(_install.Paths, _install.Files, NullLogger<SettingsStore>.Instance),
            secrets,
            new D47Settings(),
            NullLogger<SettingsService>.Instance);

        settings.Bind(CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "keys",
                Group = "Test",
                Name = "Keys",
                Summary = "One key per Commander, one for the installation.",
                Settings =
                [
                    new SettingRow
                    {
                        Key = Mine,
                        Label = "Commander key",
                        Help = "Each Commander's own.",
                        Kind = SettingKind.Secret,
                        SecretName = "commander-key",
                        Scope = SettingScope.Commander,
                    },
                    new SettingRow
                    {
                        Key = Shared,
                        Label = "Installation key",
                        Help = "Everyone's.",
                        Kind = SettingKind.Secret,
                        SecretName = "installation-key",
                    },
                ],
            },
        ]));

        return (settings, secrets);
    }

    [Fact]
    public void AKeySetUnderOneCommanderIsNotSeenUnderAnother()
    {
        var (settings, _) = Service();
        var row = settings.Find(Mine)!;

        settings.UseCommander("A", "Alice");
        Assert.Equal(SettingApplyStatus.Applied, settings.Apply(Mine, "alice-key", SettingsCaller.Panel).Status);
        Assert.True(settings.HasSecret(row));
        Assert.True(settings.HoldsSecret(row));

        settings.UseCommander("B", "Bob");
        Assert.False(settings.HasSecret(row));
        Assert.False(settings.HoldsSecret(row));

        settings.UseCommander("A", "Alice");
        Assert.True(settings.HasSecret(row));
    }

    [Fact]
    public void ItIsStoredUnderTheCommandersName()
    {
        var (settings, secrets) = Service();

        settings.UseCommander("A", "Alice");
        settings.Apply(Mine, "alice-key", SettingsCaller.Panel);

        Assert.Equal([CommanderSecret.Name("commander-key", "A")], secrets.Names);
        Assert.True(Secrets().TryGet("commander-key@A", out var value));
        Assert.Equal("alice-key", value);
    }

    [Fact]
    public void ClearingRemovesOnlyThisCommandersKey()
    {
        var (settings, _) = Service();
        var row = settings.Find(Mine)!;

        settings.UseCommander("A", "Alice");
        settings.Apply(Mine, "alice-key", SettingsCaller.Panel);
        settings.UseCommander("B", "Bob");
        settings.Apply(Mine, "bob-key", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, settings.Apply(Mine, null, SettingsCaller.Panel).Status);
        Assert.False(settings.HasSecret(row));

        settings.UseCommander("A", "Alice");
        Assert.True(settings.HasSecret(row));
    }

    [Fact]
    public void AnInstallationKeyIsSeenUnderEveryCommander()
    {
        var (settings, _) = Service();
        var row = settings.Find(Shared)!;

        settings.Apply(Shared, "everyone", SettingsCaller.Panel);
        Assert.True(settings.HasSecret(row));

        settings.UseCommander("A", "Alice");
        Assert.True(settings.HasSecret(row));
        Assert.True(settings.HoldsSecret(row));

        settings.UseCommander("B", "Bob");
        Assert.True(settings.HasSecret(row));
    }

    [Fact]
    public void WithNobodyIdentifiedTheKeyIsRefusedAndNothingIsStored()
    {
        var (settings, secrets) = Service();
        var row = settings.Find(Mine)!;

        var result = settings.Apply(Mine, "whose-key", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.Equal(
            "No Commander has been identified yet. Start Elite Dangerous or pick a Commander, then set this key.",
            result.Message);
        Assert.Empty(secrets.Names);
        Assert.False(settings.HasSecret(row));
        Assert.False(settings.HoldsSecret(row));

        Assert.Equal(SettingApplyStatus.Refused, settings.Apply(Mine, null, SettingsCaller.Panel).Status);
    }

    [Fact]
    public void TheModelCannotSetIt()
    {
        var (settings, secrets) = Service();
        settings.UseCommander("A", "Alice");

        var result = settings.Apply(Mine, "from-the-model", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.Empty(secrets.Names);
    }

    [Fact]
    public void ANewCommanderAnnouncesTheirKeyRow()
    {
        var (settings, _) = Service();
        var changed = new List<string>();
        settings.Changed += change => changed.Add(change.Key);

        settings.UseCommander("A", "Alice");
        Assert.Contains(Mine, changed);
        Assert.DoesNotContain(Shared, changed);

        changed.Clear();
        settings.UseCommander("A", "Alice");
        Assert.Empty(changed);

        settings.UseCommander("B", "Bob");
        Assert.Contains(Mine, changed);
    }

    [Fact]
    public void AMovedSecretKeepsItsValueUnderTheNewName()
    {
        var secrets = Secrets();
        secrets.Set("old", "the-value");

        Assert.True(secrets.Move("old", "new"));

        var reloaded = Secrets();
        Assert.Equal(["new"], reloaded.Names);
        Assert.True(reloaded.TryGet("new", out var value));
        Assert.Equal("the-value", value);
        Assert.False(reloaded.Has("old"));
    }

    [Fact]
    public void MovingAnAbsentSecretIsFalse()
    {
        var secrets = Secrets();
        secrets.Set("other", "kept");

        Assert.False(secrets.Move("old", "new"));
        Assert.Equal(["other"], Secrets().Names);
    }
}
