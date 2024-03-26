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
    public CapabilityStatement? CapabilityStatement { get; set; }
    public string RedirectUri { get; set; }
    public string? TokenUri { get; set; }
    public string AuthCodeUrlWithQueryString { get; set; }
}