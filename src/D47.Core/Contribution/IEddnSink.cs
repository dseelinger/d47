namespace D47.Core.Contribution;

/// <summary>Takes EDDN messages for sending.</summary>
public interface IEddnSink
{
    /// <summary>Queues the message and returns. Never blocks.</summary>
    void Post(EddnEnvelope envelope);
}
