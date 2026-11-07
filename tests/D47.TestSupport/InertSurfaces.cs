using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;

namespace D47;

/// <summary>Surfaces with every member supplied and none doing anything, for a test registry to bind.</summary>
public static class InertAboutSurface
{
    extension(AboutSurface)
    {
        public static AboutSurface Inert => new()
        {
            Build = "0.0.0-test+0000000",
            Channel = () => D47.Core.Updates.ReleaseChannel.Unknown,
            ShowChangelog = () => { },
            ShowChangelogOnline = () => { },
            AddToStartMenu = () => { },
            StartMenuWanted = () => true,
            SetUpKeys = () => { },
            ShowCommunity = () => { },
            OpenDataFolder = () => { },
            CheckForUpdate = (_, _) => Task.FromResult<string?>(null),
            InstallUpdate = (_, _) => Task.FromResult<string?>(null),
            PendingUpdateVersion = () => null,
        };
    }
}

public static class InertSpokenNamesSurface
{
    extension(SpokenNamesSurface)
    {
        public static SpokenNamesSurface Inert => new(
            () => SpokenNames.Empty,
            new MishearingWatch(),
            (_, _) => { });
    }
}

public static class InertShipsSurface
{
    extension(ShipsCapability.ShipsSurface)
    {
        public static ShipsCapability.ShipsSurface Inert => new()
        {
            Remembered = () => "Nothing is remembered in a test.",
            Rescan = () => (_, _) => Task.FromResult<string?>(null),
            Talk = () => null,
        };
    }
}
