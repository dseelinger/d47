using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace D47.Donations.R2;

/// <summary>
/// A refusal from the store, read off the structured <c>Error</c> document rather than the raw body.
/// </summary>
public sealed class R2Exception(HttpStatusCode status, string code, string message)
    : Exception($"{(int)status} {code}: {message}")
{
    public HttpStatusCode Status { get; } = status;

    public string Code { get; } = code;

    public static R2Exception From(HttpStatusCode status, string body)
    {
        try
        {
            var error = XDocument.Parse(body).Descendants().FirstOrDefault(e => e.Name.LocalName == "Error");

            if (error is not null)
            {
                return new R2Exception(
                    status,
                    error.Elements().FirstOrDefault(e => e.Name.LocalName == "Code")?.Value ?? "Unknown",
                    error.Elements().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value ?? "No message.");
            }
        }
        catch (XmlException)
        {
            // Not an S3 error document. Say so rather than quoting whatever arrived.
        }

        return new R2Exception(status, "Unknown", "The store answered with no error document.");
    }
}
