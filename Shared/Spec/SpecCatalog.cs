#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Spec;

/// <summary>A specification UdapEd teaches from.</summary>
/// <param name="Id">Short id used in section keys, e.g. "ssraa".</param>
/// <param name="ShortName">Label shown on chips, e.g. "SSRAA".</param>
/// <param name="Title">Full title.</param>
/// <param name="Version">Version cited.</param>
/// <param name="Attribution">Shown under quoted or summarized text.</param>
/// <param name="QuotesVerbatim">
/// True when the license allows short verbatim quotes in the tool (HL7 IGs are CC0). UDAP.org specifications may
/// only be reproduced in full and unmodified, so their sections carry a summary in UdapEd's own words and a link.
/// </param>
public sealed record SpecSource(string Id, string ShortName, string Title, string Version, string Attribution, bool QuotesVerbatim);

/// <summary>One citable section of a specification.</summary>
/// <param name="Key">"{source id}:{section number}", e.g. "ssraa:2.3".</param>
/// <param name="Text">
/// The normative text when <see cref="IsQuote"/> is true; otherwise a summary written for UdapEd (tables and lists
/// are summarized even for sources that may be quoted).
/// </param>
public sealed record SpecSection(string Key, SpecSource Source, string Number, string Title, string Url, string Text, bool Summary = false)
{
    /// <summary>Chip label, e.g. "SSRAA §2.3".</summary>
    public string Label => $"{Source.ShortName} §{Number}";

    /// <summary>
    /// A link to this section that scrolls to and highlights <paramref name="highlight"/> (a text fragment, what a
    /// browser's "Copy link to highlight" makes). Without highlight text it is the section link.
    /// </summary>
    public string LinkTo(string? highlight)
    {
        if (string.IsNullOrEmpty(highlight))
        {
            return Url;
        }

        // A UDAP.org section link already carries a text fragment for its heading; replace it.
        var directive = Url.IndexOf(":~:", StringComparison.Ordinal);
        var link = directive >= 0 ? Url[..directive].TrimEnd('#') : Url;
        return $"{link}{(link.Contains('#') ? "" : "#")}:~:text={SpecCatalog.TextFragment(highlight)}";
    }

    /// <summary>True when <see cref="Text"/> is the specification's own wording (with "…" marking skipped text).</summary>
    public bool IsQuote => Source.QuotesVerbatim && !Summary;
}

/// <summary>
/// Every specification section UdapEd cites. Pages and conformance checks refer to sections by key, so a link or
/// a section number is fixed here once instead of on each page.
/// </summary>
public static class SpecCatalog
{
    public static readonly SpecSource Ssraa = new(
        "ssraa",
        "SSRAA",
        "HL7 FHIR US Security for Scalable Registration, Authentication, and Authorization (UDAP Security IG)",
        "2.0.0",
        "HL7 SSRAA STU 2 (2.0.0), CC0. Quoted from the published guide.",
        QuotesVerbatim: true);

    public static readonly SpecSource UdapDcr = new(
        "udap-dcr", "UDAP DCR", "UDAP Dynamic Client Registration", "STU 1",
        "Summarized from UDAP.org. See the specification for the normative text.", QuotesVerbatim: false);

    public static readonly SpecSource UdapAuthn = new(
        "udap-authn", "UDAP AuthN", "UDAP JWT-Based Client Authentication", "STU 1",
        "Summarized from UDAP.org. See the specification for the normative text.", QuotesVerbatim: false);

    public static readonly SpecSource UdapMetadata = new(
        "udap-metadata", "UDAP Metadata", "UDAP Server Metadata", "STU 1",
        "Summarized from UDAP.org. See the specification for the normative text.", QuotesVerbatim: false);

    public static readonly SpecSource UdapTieredOAuth = new(
        "udap-to", "UDAP Tiered OAuth", "UDAP Tiered OAuth for User Authentication", "STU 1",
        "Summarized from UDAP.org. See the specification for the normative text.", QuotesVerbatim: false);

    private const string SsraaBase = "https://hl7.org/fhir/us/udap-security/STU2/";

    private static readonly SpecSection[] All =
    [
        // ---- SSRAA 2: Discovery ----
        Ssraa_("2.1", "Discovery of Endpoints", "discovery.html#discovery-of-endpoints",
            "Servers SHALL respond to GET requests to the following metadata URL by unregistered client applications and without requiring client authentication, where {baseURL} represents the base FHIR URL for the FHIR server: {baseURL}/.well-known/udap"),
        Ssraa_("2.2", "Required UDAP Metadata", "discovery.html#required-udap-metadata",
            "The metadata returned from the UDAP metadata endpoint defined above SHALL represent the server’s capabilities with respect to the UDAP workflows described in this guide. If no UDAP workflows are supported, the server SHALL return a 404 Not Found response to the metadata request. For elements that are represented by JSON arrays, clients SHALL interpret an empty array value to mean that the corresponding capability is NOT supported by the server."),
        Ssraa_("2.3", "Signed metadata elements", "discovery.html#signed-metadata-elements",
            "A server’s UDAP metadata SHALL include the signed_metadata element. The value of this element is a JWT constructed as described in Section 7.1 and containing the claims in the table below. This JWT SHALL be signed using the RS256 signature algorithm. Metadata values conveyed in the signed metadata SHALL take precedence over the corresponding values conveyed using plain JSON elements."),
        Ssraa_("2.4", "Multiple Trust Communities", "discovery.html#multiple-trust-communities",
            "… a client application SHOULD add the optional query parameter community to the metadata request URL described in Section 2.1 to indicate that it trusts certificates issued by the community identified by the parameter value. The value of the parameter SHALL be a URI as determined by the trust community for this purpose. … Servers SHALL support the community parameter."),

        // ---- SSRAA 3: Registration ----
        Ssraa_("3", "Registration", "registration.html",
            "Before FHIR data requests can be made, Client application operators SHALL register each of their applications with the Authorization Servers identified by the FHIR servers with which they wish to exchange data. Client applications SHALL use the client_id assigned by an Authorization Server in subsequent authorization and token requests to that server."),
        Ssraa_("3.1", "Software Statement", "registration.html#software-statement",
            "The software statement SHALL contain the required header elements specified in Section 7.1.3 of this guide and the JWT claims listed in the table below. The software statement SHALL be signed by the client application operator using the signature algorithm identified in the alg header of the software statement and with the private key that corresponds to the public key listed in the client’s X.509 certificate identified in the x5c header of the software statement."),
        Ssraa_("3.2.3", "Request Body", "registration.html#request-body",
            "If a new registration is successful, the Authorization Server SHALL return a registration response with a 201 Created HTTP response code as per Section 5.1 of UDAP Dynamic Client Registration, including the unique client_id assigned by the Authorization Server to that client app. … If a registration parameter has not been assigned a value by the server, the parameter SHOULD be omitted from the response; it SHOULD NOT be included with a null or empty string value. Additionally, the claims in the registration request that are defined in Section 4.1 of RFC 7519 (iss, sub, aud, exp, iat, and jti) are not registration parameters and SHOULD NOT be included in the response, as the response itself is not a JWT."),
        Ssraa_("3.3", "Inclusion of Certifications and Endorsements", "registration.html#inclusion-of-certifications-and-endorsements",
            "Authorization Servers MAY support the inclusion of certifications and endorsements by client application operators using the certifications framework outlined in UDAP Certifications and Endorsements for Client Applications. Authorization Servers SHALL ignore unsupported or unrecognized certifications."),
        Ssraa_("3.4", "Modifying and Cancelling Registrations", "registration.html#modifying-and-cancelling-registrations",
            "If an Authorization Server receives a valid registration request with a software statement that contains an empty grant_types array from a previously registered application as per the previous paragraph, the server SHOULD interpret this as a request to cancel the previous registration. A client application SHALL interpret a registration response that contains an empty grant_types array as a confirmation that the registration for the client_id listed in the response has been cancelled by the Authorization Server."),

        // ---- SSRAA 4: Consumer-facing ----
        Ssraa_("4.1", "Obtaining an authorization code (consumer-facing)", "consumer.html#obtaining-an-authorization-code",
            "Client applications SHALL request an authorization code as per Section 4.1.1 of RFC 6749, with the following additional constraints. Client applications and servers MAY optionally support UDAP Tiered OAuth for User Authentication to allow for cross-organizational or third party user authentication as described in Section 6. … Client applications and Authorization Servers SHALL conform to the additional constraints for authorization code flow found in Section 7.2 of this guide."),
        Ssraa_("4.2", "Obtaining an access token (consumer-facing)", "consumer.html#obtaining-an-access-token",
            "Client applications SHALL exchange authorization codes for access tokens as per Section 4.1.3 of RFC 6749, with the following additional options and constraints. Client applications SHALL include a code_verifier parameter and value in the token request as per Section 4.5 of RFC 7636."),
        Ssraa_("4.2.1", "Constructing Authentication Token (consumer-facing)", "consumer.html#constructing-authentication-token",
            "The client SHALL use its private key to sign an Authentication Token as described in this section, and include this JWT in the client_assertion parameter of its token request. The maximum lifetime for an Authentication Token SHALL be 5 minutes, i.e. the value of exp minus the value of iat SHALL NOT exceed 300 seconds.", summary: true),
        Ssraa_("4.2.2", "Submitting a token request (consumer-facing)", "consumer.html#submitting-a-token-request",
            "Client apps authenticating in this manner SHALL NOT include an HTTP Authorization header or client secret in the token request. The token request SHALL include the following parameters: grant_type, code, redirect_uri (conditional), client_assertion_type, client_assertion, and udap with the fixed value 1.", summary: true),
        Ssraa_("4.2.3", "Server processing of token requests (consumer-facing)", "consumer.html#server-processing-of-token-requests",
            "For all successful token requests, the Authorization Server SHALL issue access tokens with a lifetime no longer than 60 minutes."),

        // ---- SSRAA 5: Business-to-business ----
        Ssraa_("5.1", "Obtaining an authorization code (B2B)", "b2b.html#obtaining-an-authorization-code",
            "Client applications registered to use the authorization code grant SHALL request an authorization code as per Section 4.1.1 of RFC 6749, with the following additional constraints. … Client applications and Authorization Servers using the authorization code flow SHALL conform to the additional constraints for authorization code flow found in Section 7.2 of this guide."),
        Ssraa_("5.2.1", "Constructing Authentication Token (B2B)", "b2b.html#constructing-authentication-token",
            "The client SHALL use its private key to sign an Authentication Token as described in this section, and include this JWT in the client_assertion parameter of its token request. The HL7 B2B Authorization Extension Object is required for B2B client apps using the client_credentials flow; omit for client apps using the authorization_code flow. The maximum lifetime for an Authentication Token SHALL be 5 minutes.", summary: true),
        Ssraa_("5.2.1.1", "B2B Authorization Extension Object", "b2b.html#b2b-authorization-extension-object",
            "The B2B Authorization Extension Object is used by client apps following the client_credentials flow to provide additional information regarding the context under which the request for data would be authorized. The client app constructs a JSON object containing the following keys and values and includes this object in the extensions object of the Authentication JWT as the value associated with the key name hl7-b2b."),
        Ssraa_("5.2.2", "Submitting a token request (B2B)", "b2b.html#submitting-a-token-request",
            "Client apps authenticating in this manner SHALL NOT include an HTTP Authorization header or client secret in its token endpoint request. A client credentials token request SHALL include grant_type, scope, client_assertion_type, client_assertion, and udap with the fixed value 1.", summary: true),
        Ssraa_("5.2.3", "Server processing of token requests (B2B)", "b2b.html#server-processing-of-token-requests",
            "For all successful token requests, the Authorization Server SHALL issue access tokens with a lifetime no longer than 60 minutes."),

        // ---- SSRAA 6: Tiered OAuth ----
        Ssraa_("6", "Tiered OAuth for User Authentication", "user.html",
            "The UDAP Tiered OAuth workflow allows the client application to include the base URL of a preferred OpenID Connect Identity Provider (IdP) service in the initial request to the data holder’s OAuth authorization endpoint. If Tiered OAuth is supported by the data holder and the data holder trusts the IdP indicated by the client, then the data holder will request that the IdP authenticate the user, and return authentication results and optional identity information directly to the data holder using standard OIDC workflows."),
        Ssraa_("6.1", "Client Authorization Request to Data Holder", "user.html#client-authorization-request-to-data-holder",
            "A data holder that supports UDAP Tiered OAuth SHALL include \"udap\" in the array of scopes returned for the scopes_supported parameter in its UDAP metadata. To begin the workflow, the client app modifies the authorization endpoint request as follows: add udap to the list of scopes provided in the value of the scope query parameter, and add the extension query parameter idp with a value equal to the base URL of the preferred OIDC IdP.", summary: true),
        Ssraa_("6.2", "Data Holder Authentication Request to IdP", "user.html#data-holder-authentication-request-to-idp",
            "If the IdP is not trusted by the data holder, or if the data holder does not have and cannot obtain a client_id to use with the IdP, the data holder MAY reject the client app’s authorization request by returning an error as per Section 4.1.2.1 of RFC 6749, using the extension error code of invalid_idp."),

        // ---- SSRAA 7: General ----
        Ssraa_("7.1.2", "Signature algorithm identifiers", "general.html#signature-algorithm-identifiers",
            "RS256: Implementers SHALL support this algorithm. ES256: Implementers SHOULD support this algorithm. RS384 and ES384: Implementers MAY support these algorithms.", summary: true),
        Ssraa_("7.1.3", "JWT headers", "general.html#jwt-headers",
            "All JWTs defined in this guide SHALL contain a Javascript Object Signing and Encryption (JOSE) header as defined in Section 4 of RFC 7515 that conforms to the following requirements: alg (required), and x5c (required), an array of one or more strings containing the X.509 certificate or certificate chain, with the leaf certificate appearing as the first (or only) element of the array.", summary: true),
        Ssraa_("7.1.4", "JWT Claims", "general.html#jwt-claims",
            "All JWTs defined in this guide contain the iss, exp, and jti claims. … The issuer of a JWT SHALL NOT reuse the same jti value in a new JWT with the same iss value prior to the expiration of the previous JWT."),
        Ssraa_("7.1.5", "Normalization of URI values in JWTs", "general.html#normalization-of-uri-values-in-jwts",
            "Per Section 2 of RFC 7519, URI values in JWTs are compared as case-sensitive strings with no transformations or canonicalizations applied. The JWT producer SHOULD include URI values that are normalized to facilitate successful string matching by the JWT consumer. The JWT consumer is NOT REQUIRED to perform any normalization of URI values prior to making comparisons."),
        Ssraa_("7.2", "Authorization code flow", "general.html#authorization-code-flow",
            "Authorization requests submitted by client applications SHALL include the following parameters: response_type (fixed value code), client_id, redirect_uri (conditional), scope, state, code_challenge, and code_challenge_method (fixed value S256).", summary: true),
        Ssraa_("7.2.1", "The state parameter", "general.html#the-state-parameter",
            "A Client application SHALL include the state parameter in its authorization request. … Servers SHALL include the state parameter and corresponding value provided by the client application in the authorization response as per RFC 6749. The client application SHALL NOT proceed if the state parameter is not included in the authorization response or its value does not match the value provided by the client application in the corresponding authorization request."),
        Ssraa_("7.2.2", "Proof Key for Code Exchange (PKCE)", "general.html#proof-key-for-code-exchange-pkce",
            "Client applications and Authorization Servers SHALL utilize Proof Key for Code Exchange (PKCE) with code_challenge_method of S256 as defined in RFC 7636."),
        Ssraa_("7.3", "Scope negotiation", "general.html#scope-negotiation",
            "The scopes_supported metadata SHALL be present in the .well-known/udap object and SHALL list all scopes supported by the server including all supported wildcard scopes. … In registration requests and token requests, an authorization server MAY grant all scopes requested by the client or a subset thereof. It MAY also grant additional scopes that were not in the set of scopes requested by the client application."),

        // ---- UDAP.org (summaries; see QuotesVerbatim) ----
        new("udap-metadata:1", UdapMetadata, "1", "Supported metadata elements",
            Udap("udap-server-metadata-stu1", "1 Supported metadata elements"),
            "Lists the metadata elements a server publishes at .well-known/udap: the UDAP versions and profiles it supports, its endpoints, and the algorithms and certifications it accepts."),
        new("udap-metadata:3", UdapMetadata, "3", "Client validation of signed endpoints",
            Udap("udap-server-metadata-stu1", "3 Client validation of signed endpoints"),
            "Before trusting the endpoints, the client verifies the signed_metadata JWT: the signature with the leaf certificate's key, a trusted chain to an anchor with revocation checking, and that iss names the server and matches a SAN URI in the certificate."),
        new("udap-dcr:2", UdapDcr, "2", "Software statement",
            Udap("udap-dynamic-client-registration-stu1", "2. The Client App prepares a software statement"),
            "The client builds a software statement: a JWT signed with the private key of its certificate, whose x5c header carries the certificate and whose claims carry the registration parameters."),
        new("udap-dcr:4", UdapDcr, "4", "Authorization Server validation",
            Udap("udap-dynamic-client-registration-stu1", "4. Authorization Server validates request"),
            "The server checks the signature, builds a trusted chain from the client certificate to an anchor, checks iss against the certificate's SAN URIs, sub against iss, aud against its registration endpoint, the expiry, and the requested parameters."),
        new("udap-dcr:5", UdapDcr, "5", "Registration response",
            Udap("udap-dynamic-client-registration-stu1", "5. Authorization Server responds to request"),
            "A granted request returns the client_id with the registration parameters (5.1). A denied one returns an RFC 7591 error, such as unapproved_software_statement for trust failures or invalid_software_statement for bad signatures (5.2)."),
        new("udap-dcr:6", UdapDcr, "6", "Modifying or cancelling registrations",
            Udap("udap-dynamic-client-registration-stu1", "6 Modifying or Cancelling previous registrations"),
            "A client changes or cancels its registration by registering again with the same certificate identity; an empty grant_types array asks the server to cancel it."),
        new("udap-authn:5", UdapAuthn, "5", "Token request with an authentication token",
            Udap("udap-jwt-client-auth-stu1", "5. The client uses this AnT to authenticate itself"),
            "Instead of a client secret, the client signs a short-lived JWT (the authentication token) with its private key and sends it as client_assertion, with udap=1, to the token endpoint."),
        new("udap-to:2", UdapTieredOAuth, "2", "Client requests a preferred IdP",
            Udap("udap-user-auth-stu1", "2. The Client App directs the user’s browser"),
            "The client adds the udap scope and an idp parameter naming the preferred identity provider to its authorization request."),
        new("udap-to:3", UdapTieredOAuth, "3", "Data holder trusts and registers with the IdP",
            Udap("udap-user-auth-stu1", "3. Resource Holder interaction with IdP"),
            "The data holder reads the IdP's UDAP metadata, decides whether to trust it, registers with it dynamically if needed, and redirects the user to it; an untrusted IdP ends in the invalid_idp error."),
        new("udap-to:4", UdapTieredOAuth, "4", "IdP authenticates the user",
            Udap("udap-user-auth-stu1", "4. IdP interaction with the user"),
            "The IdP authenticates the user with OpenID Connect and returns a code; the data holder redeems it with its own UDAP authentication token, validates the ID token, and maps the user before asking for consent."),
    ];

    private static readonly Dictionary<string, SpecSection> ByKey = All.ToDictionary(s => s.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<SpecSection> Sections => All;

    public static SpecSection? Find(string key) => ByKey.GetValueOrDefault(key.Trim());

    /// <summary>
    /// UDAP.org pages have no section anchors, so the link carries a text fragment (#:~:text=, what a browser's
    /// "Copy link to highlight" makes) naming the section heading: the browser scrolls to it and highlights it.
    /// A browser without text-fragment support just opens the page.
    /// </summary>
    internal static string Udap(string page, string headingText) =>
        $"https://www.udap.org/{page}.html#:~:text={TextFragment(headingText)}";

    /// <summary>Percent-encodes text for a text fragment; '-', ',' and '&amp;' are syntax there, so they are encoded too.</summary>
    public static string TextFragment(string text) =>
        Uri.EscapeDataString(text).Replace("-", "%2D");

    private static SpecSection Ssraa_(string number, string title, string page, string text, bool summary = false) =>
        new($"ssraa:{number}", Ssraa, number, title, SsraaBase + page, text, summary);
}
