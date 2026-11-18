using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ARavenColonialPlanKeepsEveryFieldItSendsTests
{
    private static PlannedBody Body(string name)
    {
        using var document = Fixture.Json("raven-bodies-hr-4464-full.json");
        return Assert.Single(RavenColonialResponse.ReadBodies(document), body => body.Name == name);
    }

    private static PlannedSite Project(string? carriers = null)
    {
        using var project = Fixture.Json("raven-project-hr-4464-full.json");
        using var cargo = carriers is null ? Fixture.Json("raven-project-fc-hr-4464-full.json") : JsonDocument.Parse(carriers);
        return RavenColonialResponse.ReadProject(new PlannedSite("site", "Site", PlanStatus.Build), project, cargo);
    }

    [Fact]
    public void ABodyKeepsItsKindDistanceParentsAndFeatures()
    {
        var body = Body("HR 4464 A 2");

        Assert.Equal(6, body.Num);
        Assert.Equal("hmc", body.Type);
        Assert.Equal("High metal content world", body.SubType);
        Assert.Equal(492.636365, body.DistanceLs);
        Assert.Equal([3, 1, 0], body.Parents);
        Assert.Equal(["landable", "geo", "rings", "volcanism", "tidal"], body.Features);
        Assert.Equal(2.28917140817783, body.Gravity);
        Assert.Equal(553.200012, body.Temperature);
        Assert.Equal(-1, body.Radius);
    }

    [Fact]
    public void AnUnnamedBodyIsLeftOutAndTheSystemKeepsEveryOtherOne()
    {
        using var document = JsonDocument.Parse("""[{"name":"A","num":"2","distLS":"1.5","parents":["1",0],"type":"st"},{"num":3},{"name":"C"}]""");

        var body = Assert.Single(RavenColonialResponse.ReadBodies(document));

        Assert.Equal(2, body.Num);
        Assert.Equal(1.5, body.DistanceLs);
        Assert.Equal([1, 0], body.Parents);
        Assert.Null(body.SubType);
        Assert.Empty(body.Features);
    }

    [Fact]
    public void AProjectKeepsItsFiguresPlaceAndFeatures()
    {
        var site = Project();

        Assert.Equal("demeter", site.BuildType);
        Assert.Equal(6641, site.MaxNeed);
        Assert.Equal(0, site.SumNeed);
        Assert.True(site.Complete);
        Assert.Equal(18494801076, site.SystemAddress);
        Assert.Equal("HR 4464", site.SystemName);
        Assert.Equal([134.96875, 319.0625, -40.75], site.StarPos);
        Assert.Equal("hmc", site.BodyType);
        Assert.Equal(["volcanism", "terraformable"], site.BodyFeatures);
        Assert.Equal("pristine", site.ReserveLevel);
        Assert.Equal(DateTimeOffset.Parse("2025-05-27T06:19:13+00:00"), site.Completed);
        Assert.StartsWith("W/", site.ETag);
        Assert.Equal(3, site.Commanders);
    }

    [Fact]
    public void AProjectKeepsEachLinkedCarrierWithItsOwnCargo()
    {
        var site = Project();

        Assert.Equal(3, site.Carriers.Count);

        var carrier = Assert.Single(site.Carriers, linked => linked.MarketId == 3701007104);

        Assert.Equal("JFQ-LKH", carrier.Name);
        Assert.Equal(3826, carrier.Cargo["steel"]);
        Assert.Equal(1943, carrier.Cargo["cmmcomposite"]);
        Assert.Equal(3826, site.OnLinkedCarriers["steel"]);
        Assert.Equal(8298, site.OnLinkedCarriers["titanium"]);
    }

    [Fact]
    public void ACarrierKeepsItsAssignmentsAndRefreshTimeAndAProjectItsReadyList()
    {
        using var project = JsonDocument.Parse("""
            {"ready":["steel"],"systemFeatures":["blackHole"],"factionName":"Invented Party",
             "linkedFC":[{"marketId":"7","name":"AAA-000","displayName":"Invented","assign":["steel","water"],"lastRefresh":"2026-07-29T03:18:13+00:00"}]}
            """);

        var site = RavenColonialResponse.ReadProject(new PlannedSite("site", "Site", PlanStatus.Build), project, null);

        Assert.Equal(["steel"], site.Ready);
        Assert.Equal(["blackHole"], site.SystemFeatures);
        Assert.Equal("Invented Party", site.FactionName);

        var carrier = Assert.Single(site.Carriers);

        Assert.Equal(7, carrier.MarketId);
        Assert.Equal(["steel", "water"], carrier.Assign);
        Assert.Equal(DateTimeOffset.Parse("2026-07-29T03:18:13+00:00"), carrier.LastRefresh);
        Assert.Empty(carrier.Cargo);
    }

    [Fact]
    public void ASitesOwnFiguresWinOverTheProjectsWhereBothCarryThem()
    {
        using var project = Fixture.Json("raven-project-hr-4464-full.json");
        var site = new PlannedSite("site", "Site", PlanStatus.Build) { BuildType = "coriolis", BodyNum = 9, MarketId = 5 };

        var merged = RavenColonialResponse.ReadProject(site, project, null);

        Assert.Equal("coriolis", merged.BuildType);
        Assert.Equal(9, merged.BodyNum);
        Assert.Equal(5, merged.MarketId);
        Assert.Equal("e793a4c2-9228-4e86-a664-303cef95fc64", merged.BuildId);
    }
}
