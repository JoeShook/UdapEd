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
                () => ui.SelectAsync("registration-scope-level", "user"));
            output.WriteLine($"Registered client_id {clientId} for {E2ESettings.AuthorizationCodeSan}");

            // Authorize: build the request and send the browser to the authorization server.
            await ui.NavigateAsync("udapBusinessToBusiness", "client-select");
            await ui.SelectAsync("client-select", clientId);

            await ui.ExpandAsync("b2b-step-code-request");
            // SSRAA 2.0 requires PKCE (S256); the authorization server rejects an authorize request without it.
            await ui.CheckAsync("b2b-code-pkce");
            await ui.ByTestId("b2b-code-build-authorize").ClickAsync();

            await ui.ExpandAsync("b2b-step-code-launch");
            await Assertions.Expect(ui.ByTestId("b2b-code-launch")).ToBeEnabledAsync();
            await ui.ByTestId("b2b-code-launch").ClickAsync();

            // Sign in with one of the authorization server's test accounts, then allow consent if it is asked.
            await ui.SignInWithTestAccountAsync();
            output.WriteLine("Signed in at the authorization server with a test account");

            var allow = page.Locator("button[name='Input.Button'][value='yes']");
            var backInUdapEd = ui.ByTestId("b2b-code-build-token-request");
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

            await ui.ExpandAsync("b2b-step-code-token-request");
            await ui.ByTestId("b2b-code-build-token-request").ClickAsync();
            // The signed client assertion is built by a server round trip; Get Access Token sends it.
            await ui.WaitForTextAsync("b2b-step-code-token-request", "client_assertion=ey");

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
