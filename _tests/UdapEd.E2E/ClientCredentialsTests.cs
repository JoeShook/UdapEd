#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.RegularExpressions;
using Microsoft.Playwright;
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
        var page = await browser.NewPageAsync();
        var ui = new UdapEdPage(page);

        try
        {
            // Discovery: query the FHIR server's UDAP metadata.
            await ui.OpenAsync("/udapDiscovery", "discovery-query");
            await ui.InputOf("discovery-base-url").FillAsync(E2ESettings.FhirBaseUrl);
            if (E2ESettings.Community.Length > 0)
            {
                await ui.InputOf("discovery-community").FillAsync(E2ESettings.Community);
            }
            await ui.ByTestId("discovery-query").ClickAsync();
            await ui.WaitForTextAsync("discovery-result", "registration_endpoint");
            output.WriteLine($"Discovered UDAP metadata for {E2ESettings.FhirBaseUrl}");

            // Registration: client_credentials with the configured subject alt name.
            await ui.NavigateAsync("udapRegistration", "cert-prepackaged-menu");
            await ui.ByTestId("cert-prepackaged-menu").ClickAsync();
            await ui.ByTestId($"cert-prepackaged-{E2ESettings.ClientCertificate}").ClickAsync();
            await ui.ByTestId("cert-thumbprint").WaitForAsync();

            await ui.SelectAsync("registration-flow", "client_credentials");
            await ui.SelectAsync("registration-san", E2ESettings.ClientCredentialsSan);
            await ui.SelectAsync("registration-alg", "RS256");

            await ui.ExpandAsync("registration-step-statement");
            await ui.ByTestId("registration-build").ClickAsync();
            // Build Register fills the software statement asynchronously; the next step posts it.
            await Assertions.Expect(page.Locator("#softwareStatementTextarea"))
                .ToHaveValueAsync(new Regex("client_credentials"), new LocatorAssertionsToHaveValueOptions { Timeout = 30_000 });
            await Assertions.Expect(ui.ByTestId("registration-statement-error")).ToHaveCountAsync(0);

            await ui.ExpandAsync("registration-step-body");
            await ui.ByTestId("registration-build-body").ClickAsync();

            await ui.ExpandAsync("registration-step-register");
            await ui.ByTestId("registration-register").ClickAsync();
            var registration = await ui.WaitForTextAsync("registration-result", "client_id");
            var clientId = Regex.Match(registration, "\"client_id\"\\s*:\\s*\"(?<id>[^\"]+)\"").Groups["id"].Value;
            Assert.False(string.IsNullOrEmpty(clientId), $"No client_id in the registration response:\n{registration}");
            output.WriteLine($"Registered client_id {clientId} for {E2ESettings.ClientCredentialsSan}");

            // Token: client_credentials request signed with the registered certificate.
            await ui.NavigateAsync("udapBusinessToBusiness", "client-select");
            await ui.SelectAsync("client-select", clientId);

            await ui.ExpandAsync("b2b-step-cc-request");
            await ui.ByTestId("b2b-cc-build-token-request").ClickAsync();

            await ui.ExpandAsync("b2b-step-token");
            await ui.ByTestId("b2b-get-access-token").ClickAsync();

            var tokenOrError = page.Locator("[data-testid='b2b-access-token']:has-text('access_token'), [data-testid='b2b-token-error']").First;
            await tokenOrError.WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            Assert.Equal(0, await ui.ByTestId("b2b-token-error").CountAsync());

            var token = await ui.ByTestId("b2b-access-token").InnerTextAsync();
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
