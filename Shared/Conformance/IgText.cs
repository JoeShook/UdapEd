#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Conformance;

/// <summary>
/// The exact wording each check is based on, as rendered in the published guide. A check's chip links to its
/// section with a text fragment (#:~:text=, what a browser's "Copy link to highlight" makes), so the browser
/// scrolls to this text and highlights it. Each string must appear on the section's page exactly as written, start
/// and end on word boundaries, and be the first occurrence on that page.
/// </summary>
public static class IgText
{
    /// <summary>SSRAA §2.2, discovery.html</summary>
    public static class Metadata
    {
        public const string Required = "The metadata returned from the UDAP metadata endpoint defined above";
        public const string Versions = "A fixed array with one string element";
        public const string Core = "for UDAP JWT-Based Client Authentication";
        public const string Authz = "for UDAP Client Authorization Grants using JSON Web Tokens";
        public const string To = "for UDAP Tiered OAuth for User Authentication";
        public const string ExtPresent = "udap_authorization_extensions_supported parameter is not an empty array";
        public const string ExtSubset = "SHALL NOT include key names that are not also elements of the";
        public const string CertPresent = "udap_certifications_supported parameter is not an empty array";
        public const string CertSubset = "SHALL NOT include certification URIs that are not also elements of the";
        public const string Refresh = "grant type SHALL only be included if the";
        public const string AuthzEndpoint = "This parameter SHALL be present if the value of the grant_types_supported parameter includes the string";
        public const string AuthMethods = "Fixed array with one value";
        public const string Scopes = "SHALL list all scopes supported by the server";
    }

    /// <summary>SSRAA §2.3, discovery.html</summary>
    public static class SignedMetadata
    {
        public const string Rs256 = "This JWT SHALL be signed using the RS256 signature algorithm";
        public const string IssBase = "SHALL be equal to the server's {baseURL}";
        public const string San = "SHALL match the value of a uniformResourceIdentifier entry in the Subject Alternative Name extension of the server's certificate";
        public const string Sub = "Same as iss";
        public const string Claims = "A nonce string value that uniquely identifies this JWT";
        public const string Year = "SHALL be no more than 1 year after the value of the iat claim";
        public const string Token = "A string containing the absolute URL of the server's token endpoint";
        public const string Registration = "A string containing the absolute URL of the server's registration endpoint";
        public const string Authorization = "REQUIRED if the authorization_endpoint parameter is included in the unsigned metadata";
        public const string Precedence = "SHALL take precedence over the corresponding values conveyed using plain JSON elements";
    }

    /// <summary>SSRAA §7, general.html</summary>
    public static class Jwt
    {
        public const string Compact = "serialized using JWS Compact Serialization";
        public const string Alg = "A string identifying the signature algorithm used to sign the JWT";
        public const string AlgIds = "Signature algorithm identifiers used in this guide are defined in";
        public const string X5c = "with the leaf certificate appearing as the first";
    }

    /// <summary>SSRAA §3.1 and §3.4, registration.html</summary>
    public static class SoftwareStatement
    {
        public const string San = "SHALL match the value of a uniformResourceIdentifier entry in the Subject Alternative Name extension of the client's certificate";
        public const string Sub = "Same as iss";
        public const string Aud = "the same URL to which the registration request will be posted";
        public const string FiveMinutes = "SHALL be no more than 5 minutes after the value of the iat claim";
        public const string ClientName = "A string containing the human readable name of the client application";
        public const string AuthMethod = "Fixed string value";
        public const string Contacts = "SHALL contain at least one valid email address using the mailto scheme";
        public const string Scope = "String containing a space delimited list of scopes requested by the client application";
        public const string CancelRequest = "contains an empty grant_types array from a previously registered application";
        public const string NotBoth = "but not both";
        public const string Refresh = "SHALL NOT be present in the array unless";
        public const string RedirectPresent = "This claim SHALL be present if grant_types includes";
        public const string RedirectHttps = "Each URI SHALL use the https scheme";
        public const string ResponseTypes = "then this element SHALL have a fixed value of";
        public const string LogoPresent = "client applications SHALL include this field";
        public const string LogoHttps = "The URL SHALL use the https scheme and reference a PNG, JPG, or GIF image file";
        public const string RedirectAbsent = "and this claim SHALL be absent otherwise";
        public const string ResponseAbsent = "and SHALL be omitted otherwise";
    }

    /// <summary>SSRAA §3.2.3 and §3.4, registration.html</summary>
    public static class RegistrationResponse
    {
        public const string Created201 = "SHALL return a registration response with a 201 Created HTTP response code";
        public const string Ok200 = "it SHOULD also return a 200 OK HTTP response code";
        public const string ClientId = "including the unique client_id assigned by the Authorization Server to that client app";
        public const string Echo = "are not registration parameters and SHOULD NOT be included in the response";
        public const string Empty = "it SHOULD NOT be included with a null or empty string value";
        public const string Cancelled = "as a confirmation that the registration for the client_id listed in the response has been cancelled";
    }

    /// <summary>UDAP Dynamic Client Registration</summary>
    public static class UdapDcr
    {
        public const string Statement = "the software statement as submitted by the Client App";
        public const string Error = "If the request is denied, the Authorization Server returns an error as per Section 3.2.2 of RFC 7591";
        public const string Trust = "Denials related to trust validation";
        public const string Signature = "Denials related to invalid signatures";
    }
}
