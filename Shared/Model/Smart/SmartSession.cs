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
    public string? CapabilityStatement { get; set; }
    public string RedirectUri { get; set; }
    public string? TokenUri { get; set; }
    public string AuthCodeUrlWithQueryString { get; set; }
    public string Challenge { get; set; }
    public string Verifier { get; set; }
}