namespace D47.Core.Conversation;

/// <summary>A picture carried on a tool result, held in memory for one turn.</summary>
public sealed record ImageAttachment(byte[] Data, string MediaType, int Width, int Height)
{
    /// <summary>The picture as a <c>data:</c> URL.</summary>
    public string DataUrl => $"data:{MediaType};base64,{Convert.ToBase64String(Data)}";
}
