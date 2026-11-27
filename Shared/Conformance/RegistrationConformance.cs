#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json.Nodes;

namespace UdapEd.Shared.Conformance;

/// <summary>Checks a software statement (SSRAA 2.0 §3.1) and a registration response (§3.2.3, §3.4).</summary>
public static class RegistrationConformance
{
    private const long MaxSoftwareStatementLifetimeSeconds = 5 * 60;

    private static readonly string[] KnownAlgorithms = ["RS256", "ES256", "RS384", "ES384"];

    private static readonly string[] JwtOnlyClaims = ["iss", "sub", "aud", "exp", "iat", "jti"];

    /// <param name="softwareStatement">The signed software statement JWT.</param>
    /// <param name="registrationEndpoint">The registration endpoint from the server's metadata, when known.</param>
    public static List<ConformanceCheck> EvaluateSoftwareStatement(string softwareStatement, string? registrationEndpoint)
    {
        const string spec = "ssraa:3.1";
        var checks = new List<ConformanceCheck>();

        var jwt = JwtInspection.Parse(softwareStatement);
        checks.Add(ConformanceCheck.Of("ssraa:7.1.3", "The software statement is a compact-serialized JWT", jwt != null,
            "It could not be read as header.payload.signature"));
        if (jwt == null)
        {
            return checks;
        }

        checks.Add(ConformanceCheck.Of("ssraa:7.1.3", "The JWT header has alg", jwt.Alg != null, "alg is missing"));
        if (jwt.Alg != null && !KnownAlgorithms.Contains(jwt.Alg))
        {
            checks.Add(new ConformanceCheck("ssraa:7.1.2", "alg is one of the algorithms this guide names", CheckOutcome.Warning,
                $"alg is {jwt.Alg}; servers must support RS256, should support ES256, and may support RS384 and ES384"));
        }

        checks.Add(ConformanceCheck.Of("ssraa:7.1.3", "The JWT header carries the client certificate in x5c", jwt.X5c.Count > 0,
            "x5c is missing or empty"));

        var c = jwt.Payload;
        var iss = c.String("iss");
        var sans = jwt.LeafSanUris();
        if (sans != null)
        {
            checks.Add(ConformanceCheck.Of(spec, "iss matches a SAN URI in the x5c certificate, exactly",
                iss != null && sans.Contains(iss),
                $"iss is {iss ?? "missing"}; certificate SAN URIs: {(sans.Count == 0 ? "none" : string.Join(", ", sans))}"));
        }

        checks.Add(ConformanceCheck.Of(spec, "sub is the same as iss", iss != null && c.String("sub") == iss,
            $"sub is {c.String("sub") ?? "missing"}"));

        if (!string.IsNullOrEmpty(registrationEndpoint))
        {
            checks.Add(ConformanceCheck.Of(spec, "aud is the server's registration endpoint", c.String("aud") == registrationEndpoint,
                $"aud is {c.String("aud") ?? "missing"}; registration endpoint is {registrationEndpoint}"));
        }

        var iat = c.Long("iat");
        var exp = c.Long("exp");
        if (iat != null && exp != null)
        {
            checks.Add(ConformanceCheck.Of(spec, "exp is no more than 5 minutes after iat", exp - iat <= MaxSoftwareStatementLifetimeSeconds,
                $"exp is {DiscoveryConformance.FormatDuration(exp.Value - iat.Value)} after iat"));
        }
        else
        {
            checks.Add(new ConformanceCheck(spec, "iat and exp are present", CheckOutcome.Fail,
                "Missing: " + string.Join(", ", new[] { iat == null ? "iat" : null, exp == null ? "exp" : null }.OfType<string>())));
        }

        checks.Add(ConformanceCheck.Of(spec, "jti and client_name are present", c.Has("jti") && c.Has("client_name"),
            "Missing: " + string.Join(", ", new[] { c.Has("jti") ? null : "jti", c.Has("client_name") ? null : "client_name" }.OfType<string>())));

        checks.Add(ConformanceCheck.Of(spec, "token_endpoint_auth_method is private_key_jwt",
            c.String("token_endpoint_auth_method") == "private_key_jwt", $"Found {c.String("token_endpoint_auth_method") ?? "nothing"}"));

        var contacts = c.Strings("contacts") ?? [];
        checks.Add(ConformanceCheck.Of(spec, "contacts includes a mailto: address",
            contacts.Any(x => x.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)),
            contacts.Count == 0 ? "contacts is missing or empty" : $"contacts: {DiscoveryConformance.Quote(contacts)}"));

        checks.Add(ConformanceCheck.Of(spec, "scope is present", !string.IsNullOrWhiteSpace(c.String("scope")), "scope is missing"));

        var grants = c.Strings("grant_types");
        if (grants is { Count: 0 })
        {
            checks.Add(new ConformanceCheck("ssraa:3.4", "An empty grant_types array asks the server to cancel the registration",
                CheckOutcome.Info));
            return checks;
        }

        grants ??= [];
        var authCode = grants.Contains("authorization_code");
        var clientCredentials = grants.Contains("client_credentials");
        checks.Add(ConformanceCheck.Of(spec, "grant_types has authorization_code or client_credentials, not both", authCode ^ clientCredentials,
            grants.Count == 0 ? "grant_types is missing" : $"grant_types: {DiscoveryConformance.Quote(grants)}"));

        if (grants.Contains("refresh_token"))
        {
            checks.Add(ConformanceCheck.Of(spec, "refresh_token is requested only with authorization_code", authCode,
                "refresh_token is requested without authorization_code"));
        }

        var redirectUris = c.Strings("redirect_uris");
        var responseTypes = c.Strings("response_types");
        if (authCode)
        {
            checks.Add(ConformanceCheck.Of(spec, "authorization_code, so redirect_uris is present", redirectUris is { Count: > 0 },
                "redirect_uris is missing or empty"));
            if (redirectUris is { Count: > 0 })
            {
                var notHttps = redirectUris.Where(u => !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).ToList();
                checks.Add(ConformanceCheck.Of(spec, "Every redirect URI uses https", notHttps.Count == 0,
                    $"Not https: {DiscoveryConformance.Quote(notHttps)}"));
            }

            checks.Add(ConformanceCheck.Of(spec, "authorization_code, so response_types is [\"code\"]",
                responseTypes is { Count: 1 } && responseTypes[0] == "code",
                responseTypes == null ? "response_types is missing" : $"Found [{DiscoveryConformance.Quote(responseTypes)}]"));

            var logo = c.String("logo_uri");
            checks.Add(ConformanceCheck.Of(spec, "authorization_code, so logo_uri is present", logo != null, "logo_uri is missing"));
            if (logo != null)
            {
                var https = Uri.TryCreate(logo, UriKind.Absolute, out var logoUri) && logoUri.Scheme == Uri.UriSchemeHttps;
                checks.Add(ConformanceCheck.Of(spec, "logo_uri uses https", https, $"logo_uri is {logo}"));
                // The file type can only be guessed from the path; a server may serve a PNG from a URL without one.
                checks.Add(ConformanceCheck.Of(spec, "logo_uri points at a PNG, JPG or GIF file", HasImageExtension(logoUri),
                    $"logo_uri is {logo}; its path doesn't end in .png, .jpg or .gif", CheckOutcome.Warning));
            }
        }
        else if (clientCredentials)
        {
            checks.Add(ConformanceCheck.Of(spec, "client_credentials, so redirect_uris is absent", redirectUris == null,
                "redirect_uris is present"));
            checks.Add(ConformanceCheck.Of(spec, "client_credentials, so response_types is absent", responseTypes == null,
                "response_types is present"));
        }

        return checks;
    }

    /// <param name="httpStatus">The registration response's HTTP status code.</param>
    /// <param name="response">The registration response body.</param>
    public static List<ConformanceCheck> EvaluateRegistrationResponse(int httpStatus, JsonObject response)
    {
        var checks = new List<ConformanceCheck>();
        var grants = response.Strings("grant_types");
        var cancelled = grants is { Count: 0 };

        if (cancelled)
        {
            checks.Add(new ConformanceCheck("ssraa:3.4", "An empty grant_types array confirms the registration was cancelled",
                CheckOutcome.Info, $"client_id {response.String("client_id") ?? "(none)"}"));
        }
        else if (httpStatus == 201)
        {
            checks.Add(new ConformanceCheck("ssraa:3.2.3", "A new registration answers 201 Created", CheckOutcome.Pass));
        }
        else if (httpStatus == 200)
        {
            checks.Add(new ConformanceCheck("ssraa:3.4", "200 OK: expected when an existing registration is modified and keeps its client_id",
                CheckOutcome.Info, "A new registration SHALL answer 201 Created (§3.2.3)."));
        }
        else
        {
            checks.Add(new ConformanceCheck("ssraa:3.2.3", "A successful registration answers 201 Created (or 200 OK for a modification)",
                CheckOutcome.Warning, $"Status {httpStatus}"));
        }

        checks.Add(ConformanceCheck.Of("ssraa:3.2.3", "client_id is present", response.Has("client_id"), "client_id is missing"));

        checks.Add(ConformanceCheck.Of("udap-dcr:5", "The response includes the software statement as submitted",
            response.Has("software_statement"), "software_statement is missing"));

        var echoed = JwtOnlyClaims.Where(response.Has).ToList();
        checks.Add(ConformanceCheck.Of("ssraa:3.2.3", "JWT claims (iss, sub, aud, exp, iat, jti) are not echoed in the response",
            echoed.Count == 0, $"Present: {string.Join(", ", echoed)}", CheckOutcome.Warning));

        var empty = response
            .Where(p => p.Key != "grant_types" && (p.Value == null
                || (p.Value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0)))
            .Select(p => p.Key)
            .ToList();
        checks.Add(ConformanceCheck.Of("ssraa:3.2.3", "No parameter is returned with a null or empty string value",
            empty.Count == 0, $"Empty: {string.Join(", ", empty)}", CheckOutcome.Warning));

        return checks;
    }

    private static readonly string[] Rfc7591ErrorCodes =
        ["invalid_redirect_uri", "invalid_client_metadata", "invalid_software_statement", "unapproved_software_statement"];

    /// <summary>Checks a rejected registration's error response (UDAP DCR §5.2, RFC 7591 §3.2.2).</summary>
    public static List<ConformanceCheck> EvaluateRegistrationError(int httpStatus, string? body)
    {
        var checks = new List<ConformanceCheck>
        {
            ConformanceCheck.Of("udap-dcr:5", "A rejected registration answers 400 Bad Request", httpStatus == 400,
                $"Status {httpStatus}", CheckOutcome.Warning)
        };

        JsonObject? error = null;
        try
        {
            error = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON; reported below.
        }

        var code = error?.String("error");
        checks.Add(ConformanceCheck.Of("udap-dcr:5", "The body is a JSON error object with an error code", code != null,
            "No JSON \"error\" member in the response body"));

        if (code != null)
        {
            checks.Add(ConformanceCheck.Of("udap-dcr:5", "The error code is one RFC 7591 defines", Rfc7591ErrorCodes.Contains(code),
                $"error is {code}; expected one of {string.Join(", ", Rfc7591ErrorCodes)}", CheckOutcome.Warning));

            var meaning = code switch
            {
                "unapproved_software_statement" => "The server didn't trust the client's certificate chain.",
                "invalid_software_statement" => "The software statement's signature or claims didn't validate.",
                "invalid_client_metadata" => "A registration parameter (for example scope or grant_types) was rejected.",
                "invalid_redirect_uri" => "A redirect URI was rejected.",
                _ => null
            };
            if (meaning != null)
            {
                checks.Add(new ConformanceCheck("udap-dcr:5", $"{code}: {meaning}", CheckOutcome.Info, error?.String("error_description")));
            }
        }

        return checks;
    }

    private static bool HasImageExtension(Uri? uri) =>
        uri != null && new[] { ".png", ".jpg", ".jpeg", ".gif" }.Any(ext => uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}
