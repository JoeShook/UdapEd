#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;

public record AnchorEntry(string Name, string Url, string Description);

public static class PrePackagedAnchors
{
    public static readonly IReadOnlyList<AnchorEntry> Entries = new List<AnchorEntry>
    {
        new("FhirLabs Test CA", "https://storage.googleapis.com/crl.fhircerts.net/certs/SureFhirLabs_CA.cer",
            "Root CA for FhirLabs test communities (udap://fhirlabs1/)"),
        new("EMR Direct Test CA", "http://certs.emrdirect.com/certs/EMRDirectTestCA.crt",
            "Root CA for EMR Direct / UDAP.org test environment"),
        new("TEFCA RCE Val CA", "https://storage.googleapis.com/crl.fhircerts.net/certs/DirectTrustValRootCA.cer",
            "DirectTrust validation root CA for TEFCA RCE environments"),
        new("TEFCA Mock CA", "https://storage.googleapis.com/crl.fhircerts.net/certs/TEFCA_Test_CA.cer",
            "Mock TEFCA root CA for local development and testing"),
    };
}
