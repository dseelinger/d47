using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Each video source has its own egress entry, active while the catalogue searches it.</summary>
public class TheEgressListNamesEachVideoSourceTests
{
    [Fact]
    public void OnDefaultSettingsAllThreeAreActiveAndNameTheirHosts()
    {
        var entries = EgressDisclosure.For(new D47Settings(), llmKeyPresent: false);

        var archive = entries.Single(entry => entry.Id == EgressDisclosure.ArchiveVideos);
        var nasa = entries.Single(entry => entry.Id == EgressDisclosure.NasaVideos);
        var blender = entries.Single(entry => entry.Id == EgressDisclosure.BlenderFilms);

        Assert.True(archive.Active && nasa.Active && blender.Active);
        Assert.Equal("archive.org and *.us.archive.org", archive.Destination);
        Assert.Equal("images-api.nasa.gov and images-assets.nasa.gov", nasa.Destination);
        Assert.Equal("download.blender.org", blender.Destination);

        Assert.Contains("The words you search the video catalogue for", archive.What, StringComparison.Ordinal);
        Assert.Contains("The video you watch", archive.What, StringComparison.Ordinal);
        Assert.Contains("The words you search the video catalogue for", nasa.What, StringComparison.Ordinal);
        Assert.Contains("The video you watch", nasa.What, StringComparison.Ordinal);
        Assert.Contains("the words you search for are not sent anywhere", blender.What, StringComparison.Ordinal);
        Assert.Contains("The film you watch", blender.What, StringComparison.Ordinal);
    }

    [Fact]
    public void UntickingASourceSilencesOnlyItsEntry()
    {
        var settings = new D47Settings { Watch = new WatchSettings { Sources = [VideoSources.InternetArchive, VideoSources.Blender] } };

        var entries = EgressDisclosure.For(settings, llmKeyPresent: false);

        var nasa = entries.Single(entry => entry.Id == EgressDisclosure.NasaVideos);
        Assert.False(nasa.Active);
        Assert.Equal("NASA videos → nothing sent", nasa.Line);
        Assert.Contains("NASA Image and Video Library is not ticked", nasa.What, StringComparison.Ordinal);
        Assert.True(entries.Single(entry => entry.Id == EgressDisclosure.ArchiveVideos).Active);
        Assert.True(entries.Single(entry => entry.Id == EgressDisclosure.BlenderFilms).Active);
    }

    [Fact]
    public async Task TheToolAndTheRowsReportThemAndDropAnUntickedOne()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);
        var privacy = PrivacyCapability.Create(settings);

        var report = await privacy.Tools.Single().Handler(D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        Assert.Contains("Internet Archive videos → archive.org and *.us.archive.org", report.Content, StringComparison.Ordinal);
        Assert.Contains("NASA videos → images-api.nasa.gov and images-assets.nasa.gov", report.Content, StringComparison.Ordinal);
        Assert.Contains("Blender films → download.blender.org", report.Content, StringComparison.Ordinal);

        var row = privacy.Settings.Single(setting => setting.Key == $"egress.{EgressDisclosure.NasaVideos}");
        Assert.StartsWith("NASA videos → images-api.nasa.gov", row.Binding!.Read(settings.Current), StringComparison.Ordinal);

        settings.Replace("test", current => current with { Watch = new WatchSettings { Sources = [VideoSources.InternetArchive, VideoSources.Blender] } });

        var after = await privacy.Tools.Single().Handler(D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        Assert.Contains("NASA videos → nothing sent", after.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("images-api.nasa.gov", after.Content, StringComparison.Ordinal);
        Assert.Equal(
            "NASA Image and Video Library is not ticked in the video catalogue's sources, so nothing is searched there.",
            row.Binding!.Read(settings.Current));
    }

    [Fact]
    public void UnknownSourcesAreIgnoredAndEachIsCountedOnce()
    {
        var watch = new WatchSettings { Sources = ["loc", VideoSources.Blender, "esa", VideoSources.Nasa, VideoSources.Blender] };

        Assert.Equal([VideoSources.Nasa, VideoSources.Blender], watch.SearchedSources());
        Assert.Equal(VideoSources.All, new WatchSettings().SearchedSources());
        Assert.Empty(new WatchSettings { Sources = null! }.SearchedSources());
    }
}
