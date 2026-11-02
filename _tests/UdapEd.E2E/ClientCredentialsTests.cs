#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Xunit.Abstractions;

namespace UdapEd.E2E;

/// <summary>
/// Drives UdapEd through UDAP discovery, dynamic client registration for client_credentials,
/// and a client_credentials token request against the deployed FhirLabs servers.
/// </summary>
public class ClientCredentialsTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    [E2EFact]
    public async Task Register_And_Get_Access_Token()
    {
        var certificate = await E2ECertificate.EnsureAsync(output.WriteLine);
        var page = await browser.NewPageAsync();
        var ui = new UdapEdPage(page);

        try
        {
            // Discovery: the FHIR server's UDAP metadata for the FhirLabs community.
            await ui.DiscoverAsync();
            output.WriteLine($"Discovered UDAP metadata for {E2ESettings.FhirBaseUrl} in {E2ESettings.Community}");

            // Registration: client_credentials with the flow's own subject alt name.
            var clientId = await ui.RegisterAsync(certificate, "client_credentials", E2ESettings.ClientCredentialsSan);
            output.WriteLine($"Registered client_id {clientId} for {E2ESettings.ClientCredentialsSan}");

            // Token: client_credentials request signed with the registered certificate.
            await ui.NavigateAsync("udapBusinessToBusiness", "client-select");
            await ui.SelectAsync("client-select", clientId);

            await ui.ExpandAsync("b2b-step-cc-request");
            await ui.ByTestId("b2b-cc-build-token-request").ClickAsync();
            // The signed client assertion is built by a server round trip; Get Access Token sends it.
            // "client_assertion=ey" is the start of the generated JWT, not a static label.
            await ui.WaitForTextAsync("b2b-step-cc-request", "client_assertion=ey");

            var token = await ui.GetAccessTokenAsync();
            Assert.Contains("access_token", token);
            output.WriteLine("Received an access token for the client_credentials registration");
        }
        catch
        {
            await BrowserFixture.SaveTraceAsync(page, nameof(Register_And_Get_Access_Token));
            throw;
        }
    }
}
