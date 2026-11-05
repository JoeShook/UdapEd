#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Microsoft.Playwright;
using Xunit.Abstractions;

namespace UdapEd.E2E;

/// <summary>
/// Drives UdapEd through UDAP discovery, dynamic client registration for authorization_code, the authorization
/// server's sign-in and consent pages, and the code-for-token exchange against the deployed FhirLabs servers.
/// </summary>
public class AuthorizationCodeTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    [E2EFact]
    public async Task Register_Sign_In_And_Get_Access_Token()
    {
        var certificate = await E2ECertificate.EnsureAsync(output.WriteLine);
        var page = await browser.NewPageAsync();
        var ui = new UdapEdPage(page);

        try
        {
            await ui.DiscoverAsync();
            output.WriteLine($"Discovered UDAP metadata for {E2ESettings.FhirBaseUrl} in {E2ESettings.Community}");

            // Registration: authorization_code with user-level scopes, which selects the B2B page as redirect URI.
            var clientId = await ui.RegisterAsync(certificate, "authorization_code", E2ESettings.AuthorizationCodeSan,
                () => ui.SelectAsync(ui.Hook("registration-scope-level", ui.FieldLabeled("Level")), "user"));
            output.WriteLine($"Registered client_id {clientId} for {E2ESettings.AuthorizationCodeSan}");

            // Authorize: build the request and send the browser to the authorization server.
            await ui.NavigateAsync("udapBusinessToBusiness", "client-select");
            await ui.SelectAsync("client-select", clientId);

            await ui.ExpandAsync("b2b-step-code-request");
            // SSRAA 2.0 requires PKCE (S256); the authorization server rejects an authorize request without it.
            await ui.CheckAsync(ui.CheckBoxOf("b2b-code-pkce").Or(ui.CheckBoxLabeled("Enable PKCE")).First);
            await ui.Hook("b2b-code-build-authorize",
                page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Build Access Code Request" })).ClickAsync();

            var launchPanel = ui.Hook("b2b-step-code-launch", ui.PanelTitled("5. Run Authorize Code Request"));
            await ui.ExpandAsync(launchPanel);
            var launch = ui.Hook("b2b-code-launch",
                launchPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Run Authorize Code Request" }));
            await Assertions.Expect(launch).ToBeEnabledAsync();
            await launch.ClickAsync();

            // Sign in with one of the authorization server's test accounts, then allow consent if it is asked.
            await ui.SignInWithTestAccountAsync();
            output.WriteLine("Signed in at the authorization server with a test account");

            var allow = page.Locator("button[name='Input.Button' i][value='yes']");
            var tokenRequestPanel = ui.Hook("b2b-step-code-token-request", ui.PanelTitled("6. Build Access Token Request"));
            var backInUdapEd = ui.Hook("b2b-code-build-token-request",
                tokenRequestPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Build Access Token Request" }));
            await allow.Or(backInUdapEd).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
            if (await allow.IsVisibleAsync())
            {
                output.WriteLine("Allowing consent");
                await allow.ClickAsync();
            }

            // Back in UdapEd with the authorization code: exchange it for tokens.
            await backInUdapEd.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
            Assert.Contains("code=", page.Url);
            output.WriteLine("Returned to UdapEd with an authorization code");

            await ui.ExpandAsync(tokenRequestPanel);
            await backInUdapEd.ClickAsync();
            // The signed client assertion is built by a server round trip; Get Access Token sends it.
            await ui.WaitForTextAsync(tokenRequestPanel, "client_assertion=ey");

            var token = await ui.GetAccessTokenAsync();
            Assert.Contains("access_token", token);
            output.WriteLine("Received an access token for the authorization_code registration");
        }
        catch
        {
            await BrowserFixture.SaveTraceAsync(page, nameof(Register_Sign_In_And_Get_Access_Token));
            throw;
        }
    }
}
