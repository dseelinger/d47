namespace D47.Core.Configuration;

/// <summary>The stored name of a secret that belongs to one Commander.</summary>
public static class CommanderSecret
{
    public static string Name(string secretName, string fid) => secretName + "@" + fid;
}
