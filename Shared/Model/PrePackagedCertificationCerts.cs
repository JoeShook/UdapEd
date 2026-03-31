#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;

public record CertificationCertEntry(string Name, string CertificateName, string Description);

public static class PrePackagedCertificationCerts
{
    public static readonly IReadOnlyList<CertificationCertEntry> Entries = new List<CertificationCertEntry>
    {
        new("FhirLabs Community", "CertificateStore/fhirlabs.net.client.pfx",
            "Test client certificate for FhirLabs communities (udap://fhirlabs1/)"),
        new("EMR Direct Default", "CertificateStore/udap.emrdirect.client.certificate.p12",
            "Test client certificate for EMR Direct / UDAP.org test environment"),
        new("TEFCA RCE EMR Direct Cert", "CertificateStore/TefacaRce_EmrDirect_FhirLabs.pfx",
            "TEFCA RCE client certificate for FhirLabs community (urn:oid:2.16.840.1.113883.3.7204.1.5)"),
        new("TEFCA Mock Cert", "CertificateStore/fhirlabs.net.tefca.client.pfx",
            "Mock TEFCA client certificate for FhirLabs community (tefca://test-community)"),
        new("FhirLabs Example Certification", "CertificateStore/FhirLabsAdminCertification.pfx",
            "FhirLabs Administrator Certification for testing C&E workflows"),
    };
}
