namespace D47.Core.Web;

/// <summary>What a sign-in returned: the upload token and the Frontier customer id it belongs to, or why there is none.</summary>
public sealed record WebAppSignInResult(string? Token, string? CustomerId, string? Failure)
{
    public static WebAppSignInResult Failed(string why) => new(null, null, why);
}

/// <summary>The web app routes a desktop install calls. Never called on the tick thread.</summary>
public interface IWebAppClient
{
    /// <summary>Listens on loopback, opens the link page in the browser, takes the code it is handed back and exchanges it.</summary>
    Task<WebAppSignInResult> SignInAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the snapshot and the token; true when the web app keeps neither.</summary>
    Task<bool> DeleteSnapshotAsync(string token, CancellationToken cancellationToken);
}
