#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using UdapEd.Shared.Conformance;
using UdapEd.Shared.Spec;

namespace UtilityTests;

public class ConformanceTests
{
    private const string BaseUrl = "https://fhir.example.com/fhir/r4";
    private const string ClientUri = "https://client.example.com/apps/one";
    private const string RegistrationEndpoint = "https://auth.example.com/connect/register";

    private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ---------- Discovery (SSRAA §2.2, §2.3) ----------

    [Fact]
    public void ConformantMetadataHasNoFailures()
    {
        var checks = DiscoveryConformance.Evaluate(Metadata(), BaseUrl);

        checks.Where(c => c.Outcome == CheckOutcome.Fail).ShouldBeEmpty();
        checks.ShouldContain(c => c.Requirement.Contains("RS256") && c.Outcome == CheckOutcome.Pass);
        checks.ShouldContain(c => c.Requirement.Contains("SAN URI") && c.Outcome == CheckOutcome.Pass);
    }

    [Fact]
    public void SignedMetadataIssWithTrailingSlashDoesNotEqualBaseUrl()
    {
        var checks = DiscoveryConformance.Evaluate(Metadata(signedIss: BaseUrl + "/"), BaseUrl);

        Failed(checks, "iss equals the server's base URL").Detail!.ShouldContain(BaseUrl + "/");
        Failed(checks, "iss matches a SAN URI");
    }

    [Fact]
    public void SignedMetadataMustUseRs256()
    {
        Failed(DiscoveryConformance.Evaluate(Metadata(alg: "ES256"), BaseUrl), "RS256").Detail.ShouldBe("alg is ES256");
    }

    [Fact]
    public void SignedMetadataLivesAtMostAYear()
    {
        Failed(DiscoveryConformance.Evaluate(Metadata(lifetimeSeconds: 400L * 24 * 3600), BaseUrl), "1 year");
    }

    [Fact]
    public void ClientCredentialsRequiresUdapAuthzProfile()
    {
        var metadata = Metadata();
        metadata["udap_profiles_supported"] = new JsonArray("udap_dcr", "udap_authn");

        Failed(DiscoveryConformance.Evaluate(metadata, BaseUrl), "udap_authz");
    }

    [Fact]
    public void RequiredExtensionsMustBeSupported()
    {
        var metadata = Metadata();
        metadata["udap_authorization_extensions_required"] = new JsonArray("hl7-b2b", "tefca-ias");

        Failed(DiscoveryConformance.Evaluate(metadata, BaseUrl), "udap_authorization_extensions_required lists only")
            .Detail!.ShouldContain("tefca-ias");
    }

    [Fact]
    public void MissingRequiredElementsAreListed()
    {
        var metadata = Metadata();
        metadata.Remove("registration_endpoint_jwt_signing_alg_values_supported");

        Failed(DiscoveryConformance.Evaluate(metadata, BaseUrl), "Every required metadata element")
            .Detail!.ShouldContain("registration_endpoint_jwt_signing_alg_values_supported");
    }

    // ---------- Software statement (SSRAA §3.1) ----------

    [Fact]
    public void ConformantAuthorizationCodeStatementHasNoFailures()
    {
        var checks = RegistrationConformance.EvaluateSoftwareStatement(Statement(), RegistrationEndpoint);

        checks.Where(c => c.Outcome is CheckOutcome.Fail or CheckOutcome.Warning).ShouldBeEmpty();
    }

    [Fact]
    public void StatementLivesAtMostFiveMinutes()
    {
        Failed(RegistrationConformance.EvaluateSoftwareStatement(Statement(lifetimeSeconds: 600), RegistrationEndpoint), "5 minutes");
    }

    [Fact]
    public void StatementBreakingAuthorizationCodeRules()
    {
        var checks = RegistrationConformance.EvaluateSoftwareStatement(Statement(claims =>
        {
            claims["grant_types"] = new JsonArray("authorization_code", "client_credentials");
            claims["redirect_uris"] = new JsonArray("http://client.example.com/callback");
            claims["contacts"] = new JsonArray("https://client.example.com/support");
            claims["aud"] = "https://auth.example.com/other";
        }), RegistrationEndpoint);

        Failed(checks, "not both");
        Failed(checks, "Every redirect URI uses https");
        Failed(checks, "mailto");
        Failed(checks, "registration endpoint");
    }

    [Fact]
    public void ClientCredentialsStatementHasNoRedirectUris()
    {
        var checks = RegistrationConformance.EvaluateSoftwareStatement(Statement(claims =>
        {
            claims["grant_types"] = new JsonArray("client_credentials");
            claims.Remove("response_types");
            claims.Remove("logo_uri");
        }), RegistrationEndpoint);

        Failed(checks, "redirect_uris is absent");
    }

    [Fact]
    public void EmptyGrantTypesIsACancelRequest()
    {
        var checks = RegistrationConformance.EvaluateSoftwareStatement(
            Statement(claims => claims["grant_types"] = new JsonArray()), RegistrationEndpoint);

        checks.ShouldContain(c => c.SpecKey == "ssraa:3.4" && c.Outcome == CheckOutcome.Info);
    }

    // ---------- Registration response (SSRAA §3.2.3, UDAP DCR §5) ----------

    [Fact]
    public void RegistrationResponseThatEchoesJwtClaims()
    {
        var response = new JsonObject
        {
            ["client_id"] = "abc",
            ["iss"] = ClientUri,
            ["grant_types"] = new JsonArray("client_credentials"),
            ["client_name"] = ""
        };

        var checks = RegistrationConformance.EvaluateRegistrationResponse(201, response);

        checks.ShouldContain(c => c.Requirement.Contains("201") && c.Outcome == CheckOutcome.Pass);
        Find(checks, "not echoed").Outcome.ShouldBe(CheckOutcome.Warning);
        Find(checks, "null or empty").Detail!.ShouldContain("client_name");
        Failed(checks, "software statement as submitted");
    }

    [Fact]
    public void RegistrationErrorIsExplained()
    {
        var checks = RegistrationConformance.EvaluateRegistrationError(400,
            """{"error":"unapproved_software_statement","error_description":"untrusted"}""");

        checks.Where(c => c.Outcome is CheckOutcome.Fail or CheckOutcome.Warning).ShouldBeEmpty();
        checks.ShouldContain(c => c.Outcome == CheckOutcome.Info && c.Requirement.Contains("didn't trust"));
    }

    // ---------- Links into the guide ----------

    [Fact]
    public void EveryCheckLinksToItsTextInTheGuide()
    {
        var checks = DiscoveryConformance.Evaluate(Metadata(), BaseUrl)
            .Concat(RegistrationConformance.EvaluateSoftwareStatement(Statement(), RegistrationEndpoint))
            .Concat(RegistrationConformance.EvaluateRegistrationResponse(201, new JsonObject { ["client_id"] = "abc" }))
            .Concat(RegistrationConformance.EvaluateRegistrationError(400, """{"error":"invalid_software_statement"}"""));

        checks.ShouldAllBe(c => c.Highlight != null && c.Highlight.Start.Length > 0);
    }

    [Fact]
    public void HighlightLinksUseATextFragment()
    {
        SpecCatalog.Find("ssraa:2.2")!.LinkTo("A fixed array with one string element")
            .ShouldBe("https://hl7.org/fhir/us/udap-security/STU2/discovery.html#required-udap-metadata:~:text=A%20fixed%20array%20with%20one%20string%20element");

        // UDAP.org links already point at their heading with a text fragment; a highlight replaces it.
        SpecCatalog.Find("udap-dcr:5")!.Url.ShouldContain("#:~:text=5.%20Authorization%20Server%20responds%20to%20request");
        SpecCatalog.Find("udap-dcr:5")!.LinkTo("the software statement as submitted by the Client App")
            .ShouldBe("https://www.udap.org/udap-dynamic-client-registration-stu1.html#:~:text=the%20software%20statement%20as%20submitted%20by%20the%20Client%20App");

        // A parameter's row: its name in the first column, picked out by the badge after it.
        SpecCatalog.Find("ssraa:2.2")!.LinkTo(IgText.Metadata.Versions)
            .ShouldEndWith("#required-udap-metadata:~:text=udap_versions_supported,-required");

        // '-' is text-fragment syntax, so it is encoded.
        SpecCatalog.TextFragment("JWT-Based").ShouldBe("JWT%2DBased");
    }

    // ---------- Catalog ----------

    [Fact]
    public void SpecTextHighlightsKeywordsAndEncodesHtml()
    {
        var html = SpecText.Highlight("Servers SHALL NOT <script> and MAY").Value;

        html.ShouldContain("<span class=\"udaped-rfc2119\">SHALL NOT</span>");
        html.ShouldContain("<span class=\"udaped-rfc2119\">MAY</span>");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void EveryCatalogKeyIsUniqueAndLinksToHttps()
    {
        SpecCatalog.Sections.Select(s => s.Key).ShouldBeUnique();
        SpecCatalog.Sections.ShouldAllBe(s => s.Url.StartsWith("https://"));
    }

    [Fact]
    public void EverySpecKeyUsedInTheAppIsInTheCatalog()
    {
        var shared = Path.Combine(RepoRoot(), "Shared");
        var keyPattern = new Regex("\"((?:ssraa|udap-dcr|udap-authn|udap-metadata|udap-to):[0-9.]+)\"");
        var notesPattern = new Regex("Keys=\"([^\"]+)\"");

        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(shared, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".razor") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in keyPattern.Matches(text)) used.Add(m.Groups[1].Value);
            foreach (Match m in notesPattern.Matches(text))
            {
                foreach (var key in m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    used.Add(key);
                }
            }
        }

        used.ShouldNotBeEmpty();
        used.Where(k => SpecCatalog.Find(k) == null).ShouldBeEmpty();
    }

    // ---------- helpers ----------

    private static ConformanceCheck Find(IEnumerable<ConformanceCheck> checks, string requirementContains) =>
        checks.Single(c => c.Requirement.Contains(requirementContains));

    private static ConformanceCheck Failed(IEnumerable<ConformanceCheck> checks, string requirementContains)
    {
        var check = Find(checks, requirementContains);
        check.Outcome.ShouldBe(CheckOutcome.Fail, check.Requirement);
        return check;
    }

    private static JsonObject Metadata(string? signedIss = null, string alg = "RS256", long lifetimeSeconds = 3600)
    {
        var iss = signedIss ?? BaseUrl;
        var signed = Jwt(alg, BaseUrl, new JsonObject
        {
            ["iss"] = iss,
            ["sub"] = iss,
            ["iat"] = Now,
            ["exp"] = Now + lifetimeSeconds,
            ["jti"] = Guid.NewGuid().ToString(),
            ["authorization_endpoint"] = "https://auth.example.com/connect/authorize",
            ["token_endpoint"] = "https://auth.example.com/connect/token",
            ["registration_endpoint"] = RegistrationEndpoint
        });

        return new JsonObject
        {
            ["udap_versions_supported"] = new JsonArray("1"),
            ["udap_profiles_supported"] = new JsonArray("udap_dcr", "udap_authn", "udap_authz", "udap_to"),
            ["udap_authorization_extensions_supported"] = new JsonArray("hl7-b2b"),
            ["udap_authorization_extensions_required"] = new JsonArray("hl7-b2b"),
            ["udap_certifications_supported"] = new JsonArray(),
            ["grant_types_supported"] = new JsonArray("authorization_code", "refresh_token", "client_credentials"),
            ["scopes_supported"] = new JsonArray("openid", "udap", "system/Patient.read"),
            ["authorization_endpoint"] = "https://auth.example.com/connect/authorize",
            ["token_endpoint"] = "https://auth.example.com/connect/token",
            ["token_endpoint_auth_methods_supported"] = new JsonArray("private_key_jwt"),
            ["token_endpoint_auth_signing_alg_values_supported"] = new JsonArray("RS256", "ES256"),
            ["registration_endpoint"] = RegistrationEndpoint,
            ["registration_endpoint_jwt_signing_alg_values_supported"] = new JsonArray("RS256", "ES256"),
            ["signed_metadata"] = signed
        };
    }

    private static string Statement(Action<JsonObject>? change = null, long lifetimeSeconds = 300)
    {
        var claims = new JsonObject
        {
            ["iss"] = ClientUri,
            ["sub"] = ClientUri,
            ["aud"] = RegistrationEndpoint,
            ["iat"] = Now,
            ["exp"] = Now + lifetimeSeconds,
            ["jti"] = Guid.NewGuid().ToString(),
            ["client_name"] = "Test App",
            ["redirect_uris"] = new JsonArray("https://client.example.com/callback"),
            ["contacts"] = new JsonArray("mailto:ops@example.com"),
            ["logo_uri"] = "https://client.example.com/logo.png",
            ["grant_types"] = new JsonArray("authorization_code", "refresh_token"),
            ["response_types"] = new JsonArray("code"),
            ["token_endpoint_auth_method"] = "private_key_jwt",
            ["scope"] = "user/Patient.read"
        };
        change?.Invoke(claims);
        return Jwt("RS256", ClientUri, claims);
    }

    /// <summary>A JWT whose x5c holds a certificate with the given SAN URI. The signature is a placeholder: the checks don't verify it.</summary>
    private static string Jwt(string alg, string sanUri, JsonObject claims)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=conformance-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddUri(new Uri(sanUri));
        request.CertificateExtensions.Add(san.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var header = new JsonObject { ["alg"] = alg, ["x5c"] = new JsonArray(Convert.ToBase64String(certificate.RawData)) };
        return $"{Encode(header)}.{Encode(claims)}.c2lnbmF0dXJl";
    }

    private static string Encode(JsonObject json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json.ToJsonString()));

    // From this source file rather than the test output, which may be built outside the repository.
    private static string RepoRoot([CallerFilePath] string sourceFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "UdapEd.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("UdapEd.sln not found above the test source folder");
    }
}
