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
    /// <summary>
    /// UdapEd under test, e.g. https://localhost:7041 or https://fhirlabs-udaped-v46zp6zteq-uw.a.run.app.
    /// The tests are skipped when it is not set.
    /// </summary>
    public static string? UdapEdUrl => Environment.GetEnvironmentVariable("UDAPED_URL")?.TrimEnd('/');

    /// <summary>FHIR server whose UDAP metadata starts every flow.</summary>
    public static string FhirBaseUrl => Get("UDAPED_FHIR_BASE_URL", "https://fhirlabs.net/fhir/r4");

    /// <summary>UDAP community to query; empty means the server's default community.</summary>
    public static string Community => Get("UDAPED_COMMUNITY", "");

    /// <summary>UdapEd's pre-packaged client certificate used for the registrations.</summary>
    public static string ClientCertificate => Get("UDAPED_CLIENT_CERT", "FhirLabs Community");

    /// <summary>Subject alt name the client_credentials registration uses.</summary>
    public static string ClientCredentialsSan => Get("UDAPED_CC_SAN", "https://fhirlabs.net:7016/fhir/r4");

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
