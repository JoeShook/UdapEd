#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.E2E;

/// <summary>
/// End-to-end settings, read from environment variables so the same tests run against a local or deployed UdapEd.
/// </summary>
public static class E2ESettings
{
    /// <summary>The deployed UdapEd.</summary>
    public const string DeployedUdapEdUrl = "https://fhirlabs-udaped-v46zp6zteq-uw.a.run.app";

    /// <summary>
    /// UdapEd under test, e.g. https://localhost:7041 or the deployed UdapEd.
    /// The tests are skipped when it is not set.
    /// </summary>
    public static string? UdapEdUrl => Environment.GetEnvironmentVariable("UDAPED_URL")?.TrimEnd('/');

    /// <summary>FHIR server whose UDAP metadata starts every flow.</summary>
    public static string FhirBaseUrl => Get("UDAPED_FHIR_BASE_URL", "https://fhirlabs.net/fhir/r4");

    /// <summary>
    /// UDAP community to discover. The E2E certificate is issued by the SureFhirLabs CA, which anchors
    /// udap://fhirlabs.net/; fhirlabs.net's default community is a different one (udap://stage.healthtogo.me/).
    /// </summary>
    public static string Community => Get("UDAPED_COMMUNITY", "udap://fhirlabs.net/");

    /// <summary>Subject alt name the client_credentials registration uses.</summary>
    public static string ClientCredentialsSan => Get("UDAPED_CC_SAN", "https://fhirlabs.net/udaped-e2e/client-credentials");

    /// <summary>Subject alt name the authorization_code registration uses.</summary>
    public static string AuthorizationCodeSan => Get("UDAPED_CODE_SAN", "https://fhirlabs.net/udaped-e2e/authorization-code");

    /// <summary>Subject alt name the Tiered OAuth registration uses.</summary>
    public static string TieredOAuthSan => Get("UDAPED_TIERED_SAN", "https://fhirlabs.net/udaped-e2e/tiered-oauth");

    /// <summary>The E2E client certificate's SANs, one per flow, so no flow overwrites another's registration.</summary>
    public static IReadOnlyList<string> CertificateSans => [ClientCredentialsSan, AuthorizationCodeSan, TieredOAuthSan];

    /// <summary>
    /// The E2E client certificate (PKCS#12). Kept outside the repository; issued on first use.
    /// </summary>
    public static string CertificatePath => Get("UDAPED_E2E_CERT",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UdapEd.E2E", "udaped-e2e-client.p12"));

    /// <summary>Password of UdapEd-issued FhirLabs certificates.</summary>
    public static string CertificatePassword => Get("UDAPED_E2E_CERT_PASSWORD", "udap-test");

    /// <summary>
    /// UdapEd that issues the E2E certificate. Issuing needs the SureFhirLabs CA in GCP, which the deployed UdapEd can read.
    /// </summary>
    public static string CertificateIssuerUrl => Get("UDAPED_CERT_ISSUER_URL", DeployedUdapEdUrl).TrimEnd('/');

    /// <summary>Show the browser instead of running headless (UDAPED_HEADED=1).</summary>
    public static bool Headed => Environment.GetEnvironmentVariable("UDAPED_HEADED") == "1";

    /// <summary>Milliseconds to pause between browser actions when watching a run.</summary>
    public static float SlowMo => float.TryParse(Environment.GetEnvironmentVariable("UDAPED_SLOWMO"), out var ms) ? ms : 0;

    private static string Get(string name, string defaultValue) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : defaultValue;
}

/// <summary>
/// A test that drives UdapEd against live servers. Skipped unless UDAPED_URL is set.
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (E2ESettings.UdapEdUrl == null)
        {
            Skip = "Set UDAPED_URL to the UdapEd under test to run end-to-end tests.";
        }
    }
}
