using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace UdapEd.Shared.Model.Smart;

public class SmartSession
{
    public string State { get; }

    public SmartSession(string state)
    {
        State = state;
    }

    public string ServiceUri { get; set; }

    /// <summary>
    /// Kept out of the saved app state: a server's whole CapabilityStatement can run to megabytes, and every
    /// app-state save serialized it again. Nothing reads it back after the launch.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CapabilityStatement { get; set; }
    public string RedirectUri { get; set; }
    public string? TokenUri { get; set; }
    public string AuthCodeUrlWithQueryString { get; set; }
    public string Challenge { get; set; }
    public string Verifier { get; set; }
}