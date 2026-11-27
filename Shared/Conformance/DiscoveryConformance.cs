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

/// <summary>Checks a server's .well-known/udap metadata against SSRAA 2.0 §2.2 and §2.3.</summary>
public static class DiscoveryConformance
{
    private static readonly string[] RequiredElements =
    [
        "udap_versions_supported",
        "udap_profiles_supported",
        "udap_authorization_extensions_supported",
        "udap_certifications_supported",
        "grant_types_supported",
        "scopes_supported",
        "token_endpoint",
        "token_endpoint_auth_methods_supported",
        "token_endpoint_auth_signing_alg_values_supported",
        "registration_endpoint",
        "registration_endpoint_jwt_signing_alg_values_supported",
        "signed_metadata"
    ];

    /// <summary>A year, allowing for a leap year.</summary>
    private const long MaxSignedMetadataLifetimeSeconds = 366L * 24 * 60 * 60;

    /// <param name="metadata">The metadata document.</param>
    /// <param name="baseUrl">The FHIR base URL the metadata was requested for (before /.well-known/udap).</param>
    public static List<ConformanceCheck> Evaluate(JsonObject metadata, string? baseUrl)
    {
        var checks = new List<ConformanceCheck>();
        EvaluateRequiredMetadata(metadata, checks);
        EvaluateSignedMetadata(metadata, baseUrl, checks);
        return checks;
    }

    private static void EvaluateRequiredMetadata(JsonObject m, List<ConformanceCheck> checks)
    {
        const string spec = "ssraa:2.2";

        var missing = RequiredElements.Where(e => !m.Has(e)).ToList();
        checks.Add(ConformanceCheck.Of(spec, "Every required metadata element is present", missing.Count == 0,
            $"Missing: {string.Join(", ", missing)}") with { Highlight = IgText.Metadata.Required });

        var versions = m.Strings("udap_versions_supported");
        if (versions != null)
        {
            checks.Add(ConformanceCheck.Of(spec, "udap_versions_supported is [\"1\"]",
                versions.Count == 1 && versions[0] == "1", $"Found [{Quote(versions)}]") with { Highlight = IgText.Metadata.Versions });
        }

        var grants = m.Strings("grant_types_supported") ?? [];
        var profiles = m.Strings("udap_profiles_supported");
        if (profiles != null)
        {
            var core = new[] { "udap_dcr", "udap_authn" }.Where(p => !profiles.Contains(p)).ToList();
            checks.Add(ConformanceCheck.Of(spec, "udap_profiles_supported includes udap_dcr and udap_authn", core.Count == 0,
                $"Missing {Quote(core)}") with { Highlight = IgText.Metadata.Core });

            if (grants.Contains("client_credentials"))
            {
                checks.Add(ConformanceCheck.Of(spec, "client_credentials is offered, so udap_profiles_supported includes udap_authz",
                    profiles.Contains("udap_authz"), "udap_authz is missing") with { Highlight = IgText.Metadata.Authz });
            }

            if ((m.Strings("scopes_supported") ?? []).Contains("udap"))
            {
                checks.Add(ConformanceCheck.Of(spec, "The udap scope signals Tiered OAuth (§6.1), so udap_profiles_supported includes udap_to",
                    profiles.Contains("udap_to"), "udap_to is missing") with { Highlight = IgText.Metadata.To });
            }
        }

        AddSubsetCheck(m, "udap_authorization_extensions_supported", "udap_authorization_extensions_required", checks);
        AddSubsetCheck(m, "udap_certifications_supported", "udap_certifications_required", checks);

        if (grants.Count > 0)
        {
            if (grants.Contains("refresh_token"))
            {
                checks.Add(ConformanceCheck.Of(spec, "refresh_token is offered only together with authorization_code",
                    grants.Contains("authorization_code"), "refresh_token is listed without authorization_code") with { Highlight = IgText.Metadata.Refresh });
            }

            if (grants.Contains("authorization_code"))
            {
                checks.Add(ConformanceCheck.Of(spec, "authorization_code is offered, so authorization_endpoint is present",
                    m.Has("authorization_endpoint"), "authorization_endpoint is missing") with { Highlight = IgText.Metadata.AuthzEndpoint });
            }
        }

        var authMethods = m.Strings("token_endpoint_auth_methods_supported");
        if (authMethods != null)
        {
            checks.Add(ConformanceCheck.Of(spec, "token_endpoint_auth_methods_supported is [\"private_key_jwt\"]",
                authMethods.Count == 1 && authMethods[0] == "private_key_jwt", $"Found [{Quote(authMethods)}]") with { Highlight = IgText.Metadata.AuthMethods });
        }

        var scopes = m.Strings("scopes_supported");
        if (scopes != null)
        {
            checks.Add(ConformanceCheck.Of("ssraa:7.3", "scopes_supported lists at least one scope", scopes.Count > 0,
                "scopes_supported is empty") with { Highlight = IgText.Metadata.Scopes });
        }
    }

    // §2.2: X_required SHALL be present when X_supported is not empty, and only list values from X_supported.
    private static void AddSubsetCheck(JsonObject m, string supportedName, string requiredName, List<ConformanceCheck> checks)
    {
        var supported = m.Strings(supportedName);
        if (supported is not { Count: > 0 })
        {
            return;
        }

        var required = m.Strings(requiredName);
        checks.Add(ConformanceCheck.Of("ssraa:2.2", $"{supportedName} is not empty, so {requiredName} is present",
            required != null, $"{requiredName} is missing") with { Highlight = supportedName == "udap_certifications_supported" ? IgText.Metadata.CertPresent : IgText.Metadata.ExtPresent });

        if (required != null)
        {
            var extra = required.Where(r => !supported.Contains(r)).ToList();
            checks.Add(ConformanceCheck.Of("ssraa:2.2", $"{requiredName} lists only values from {supportedName}",
                extra.Count == 0, $"Not in {supportedName}: {Quote(extra)}") with { Highlight = supportedName == "udap_certifications_supported" ? IgText.Metadata.CertSubset : IgText.Metadata.ExtSubset });
        }
    }

    private static void EvaluateSignedMetadata(JsonObject m, string? baseUrl, List<ConformanceCheck> checks)
    {
        const string spec = "ssraa:2.3";

        var signed = m.String("signed_metadata");
        if (signed == null)
        {
            return; // Already reported as a missing required element.
        }

        var jwt = JwtInspection.Parse(signed);
        checks.Add(ConformanceCheck.Of("ssraa:7.1.3", "signed_metadata is a compact-serialized JWT", jwt != null,
            "It could not be read as header.payload.signature") with { Highlight = IgText.Jwt.Compact });
        if (jwt == null)
        {
            return;
        }

        checks.Add(ConformanceCheck.Of(spec, "signed_metadata is signed with RS256", jwt.Alg == "RS256",
            $"alg is {jwt.Alg ?? "missing"}") with { Highlight = IgText.SignedMetadata.Rs256 });
        checks.Add(ConformanceCheck.Of("ssraa:7.1.3", "The JWT header carries the certificate in x5c", jwt.X5c.Count > 0,
            "x5c is missing or empty") with { Highlight = IgText.Jwt.X5c });

        var claims = jwt.Payload;
        var iss = claims.String("iss");

        if (string.IsNullOrEmpty(baseUrl))
        {
            checks.Add(new ConformanceCheck(spec, "iss equals the server's base URL", CheckOutcome.Info, "No base URL to compare with") with { Highlight = IgText.SignedMetadata.IssBase });
        }
        else
        {
            // §7.1.5: compared as exact strings, so a trailing slash or a different case is a mismatch.
            checks.Add(ConformanceCheck.Of(spec, "iss equals the server's base URL, exactly", iss == baseUrl,
                $"iss is {iss ?? "missing"}; base URL is {baseUrl}") with { Highlight = IgText.SignedMetadata.IssBase });
        }

        var sans = jwt.LeafSanUris();
        if (sans == null)
        {
            checks.Add(new ConformanceCheck(spec, "iss matches a SAN URI in the x5c certificate", CheckOutcome.Info,
                "The certificate could not be read") with { Highlight = IgText.SignedMetadata.San });
        }
        else
        {
            checks.Add(ConformanceCheck.Of(spec, "iss matches a SAN URI in the x5c certificate, exactly",
                iss != null && sans.Contains(iss),
                $"iss is {iss ?? "missing"}; certificate SAN URIs: {(sans.Count == 0 ? "none" : string.Join(", ", sans))}") with { Highlight = IgText.SignedMetadata.San });
        }

        checks.Add(ConformanceCheck.Of(spec, "sub is the same as iss", iss != null && claims.String("sub") == iss,
            $"sub is {claims.String("sub") ?? "missing"}") with { Highlight = IgText.SignedMetadata.Sub });

        var iat = claims.Long("iat");
        var exp = claims.Long("exp");
        checks.Add(ConformanceCheck.Of(spec, "iat, exp and jti are present", iat != null && exp != null && claims.Has("jti"),
            "Missing: " + string.Join(", ", new[] { iat == null ? "iat" : null, exp == null ? "exp" : null, claims.Has("jti") ? null : "jti" }.OfType<string>())) with { Highlight = IgText.SignedMetadata.Claims });
        if (iat != null && exp != null)
        {
            checks.Add(ConformanceCheck.Of(spec, "exp is no more than 1 year after iat", exp - iat <= MaxSignedMetadataLifetimeSeconds,
                $"exp is {FormatDuration(exp.Value - iat.Value)} after iat") with { Highlight = IgText.SignedMetadata.Year });
        }

        foreach (var endpoint in new[] { "token_endpoint", "registration_endpoint", "authorization_endpoint" })
        {
            var plain = m.String(endpoint);
            var inSigned = claims.String(endpoint);
            var required = endpoint != "authorization_endpoint" || plain != null;
            if (required)
            {
                checks.Add(ConformanceCheck.Of(spec, $"signed_metadata contains {endpoint}", inSigned != null, "Missing") with { Highlight = endpoint switch { "token_endpoint" => IgText.SignedMetadata.Token, "registration_endpoint" => IgText.SignedMetadata.Registration, _ => IgText.SignedMetadata.Authorization } });
            }

            if (plain != null && inSigned != null && plain != inSigned)
            {
                checks.Add(new ConformanceCheck(spec, $"{endpoint} differs between the plain and signed metadata", CheckOutcome.Warning,
                    $"Plain: {plain}; signed: {inSigned}. The signed value takes precedence.") with { Highlight = IgText.SignedMetadata.Precedence });
            }
        }
    }

    internal static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"\"{v}\""));

    internal static string FormatDuration(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalDays >= 2 ? $"{span.TotalDays:0.#} days"
            : span.TotalHours >= 2 ? $"{span.TotalHours:0.#} hours"
            : span.TotalMinutes >= 2 ? $"{span.TotalMinutes:0.#} minutes"
            : $"{seconds} seconds";
    }
}
