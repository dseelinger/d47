using D47.Llm;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>A request refused for a field it carried retries without it, whichever request recorded the demotion first.</summary>
[Collection(nameof(EndpointDemotionCollection))]
public sealed class ARefusalAnotherRequestRecordedIsStillRetriedTests
{
    private const string Address = "http://127.0.0.1:1/v1";

    public ARefusalAnotherRequestRecordedIsStillRetriedTests() => EndpointDemotions.Clear();

    [Fact]
    public void TheFirstRefusalOfAFieldIsRetried()
    {
        Assert.True(EndpointDemotions.DemoteSent(Address, Demotable.Sampling, []));
        Assert.False(EndpointDemotions.Allows(Address, Demotable.Sampling));
    }

    [Fact]
    public void ARefusalAfterAnotherRequestDemotedTheFieldIsRetried()
    {
        var before = EndpointDemotions.RefusedBy(Address);

        EndpointDemotions.Demote(Address, Demotable.Sampling);

        Assert.True(EndpointDemotions.DemoteSent(Address, Demotable.Sampling, before));
    }

    [Fact]
    public void ARefusalOfAFieldDemotedBeforeTheRequestWasBuiltIsNotRetried()
    {
        EndpointDemotions.Demote(Address, Demotable.Sampling);

        Assert.False(EndpointDemotions.DemoteSent(Address, Demotable.Sampling, EndpointDemotions.RefusedBy(Address)));
    }
}
