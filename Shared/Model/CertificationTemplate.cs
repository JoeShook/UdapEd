#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;

namespace UdapEd.Shared.Model;

public record CertificationTemplate(
    string Name,
    string CertificationName,
    string CertificationDescription,
    List<string> CertificationUris,
    Dictionary<string, JsonElement>? AdditionalClaims = null);

public static class CertificationTemplates
{
    public static readonly IReadOnlyList<CertificationTemplate> Entries = new List<CertificationTemplate>
    {
        new("Default (FhirLabs Admin)",
            "FhirLabs Administrator Certification",
            "Application can perform all CRUD operations against the FHIR server.",
            new List<string> { "https://certifications.fhirlabs.net/criteria/admin-2024.7" }),

        new("TEFCA Basic App",
            "TEFCA Basic App Certification",
            "TEFCA Basic App Certification for qualified health information network exchange.",
            new List<string> { "https://rce.sequoiaproject.org/udap/profiles/basic-app-certification" },
            new Dictionary<string, JsonElement>
            {
                ["exchange_purposes"] = JsonSerializer.SerializeToElement(
                    new[] { "urn:oid:2.16.840.1.113883.3.7204.1.5.2.1#T-IAS" }),
                ["home_community_id"] = JsonSerializer.SerializeToElement(
                    "urn:oid:2.16.840.1.113883.3.2054.2.4"),
            }),
    };
}
