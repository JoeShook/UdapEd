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
/// Drives UdapEd through Tiered OAuth: registration for authorization_code with the udap scope, an authorize request
/// that names an OpenID Connect IdP, the authorization server's hand-off to that IdP, sign-in at the IdP, and the
/// code-for-token exchange, against the deployed FhirLabs authorization server and IdP.
/// </summary>
public class TieredOAuthTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    [E2EFact]
    public async Task Register_Sign_In_At_IdP_And_Get_Access_Token()
    {
        var certificate = await E2ECertificate.EnsureAsync(output.WriteLine);
        var page = await browser.NewPageAsync();
        var ui = new UdapEdPage(page);

        try
        {
            await ui.DiscoverAsync();
            output.WriteLine($"Discovered UDAP metadata for {E2ESettings.FhirBaseUrl} in {E2ESettings.Community}");

            // Registration: authorization_code, user-level scopes, and Tiered OAuth, which adds the udap and openid
            // scopes and the Tiered OAuth page as a redirect URI.
            var clientId = await ui.RegisterAsync(certificate, "authorization_code", E2ESettings.TieredOAuthSan, async () =>
            {
                await ui.SelectAsync(ui.Hook("registration-scope-level", ui.FieldLabeled("Level")), "user");
                await ui.CheckAsync(ui.Either(ui.CheckBoxOf("registration-tiered-oauth"), ui.CheckBoxLabeled("Tiered Oauth")));
            });
            output.WriteLine($"Registered client_id {clientId} for {E2ESettings.TieredOAuthSan}");

            // Tiered OAuth page: the client and the IdP the user picks.
            await ui.NavigateAsync("udapTieredOAuth", "client-select");
            await ui.SelectAsync("client-select", clientId);

            var idpField = ui.Either(ui.InputOf("tiered-idp"), ui.FieldLabeled("OpenID Connect IdP").Locator("input").First);
            await idpField.FillAsync(E2ESettings.TieredIdP);
            await idpField.PressAsync("Tab"); // the IdP is saved when the field loses focus

            var requestPanel = ui.Hook("tiered-step-code-request", ui.PanelTitled("2. Build Authorization Code Request"));
            await ui.ExpandAsync(requestPanel);
            // SSRAA 2.0 requires PKCE (S256).
            await ui.CheckAsync(ui.Either(ui.CheckBoxOf("tiered-code-pkce"),
                requestPanel.Locator("label.mud-checkbox:has-text('Enable PKCE') input[type='checkbox']").First));
            await ui.Hook("tiered-code-build-authorize",
                requestPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Build Access Code Request" })).ClickAsync();

            var launchPanel = ui.Hook("tiered-step-code-launch", ui.PanelTitled("4. Run Authorize Code Request"));
            await ui.ExpandAsync(launchPanel);
            var launch = ui.Hook("tiered-code-launch",
                launchPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Run Authorize Code Request" }));
            await Assertions.Expect(launch).ToBeEnabledAsync();
            await launch.ClickAsync();

            // The authorization server hands off to the IdP named in the request.
            var idpHost = new Uri(E2ESettings.TieredIdP).Host;
            var handOff = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = $"Continue to {idpHost}" });
            await handOff.WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
            output.WriteLine($"Authorization server offers the hand-off to {idpHost}");
            await handOff.ClickAsync();

            await ui.SignInWithTestAccountAsync();
            output.WriteLine($"Signed in at {new Uri(page.Url).Host} with a test account");

            // Consent may be asked at the IdP, at the authorization server, or not at all.
            var tokenRequestPanel = ui.Hook("tiered-step-code-token-request", ui.PanelTitled("5. Build Access Token Request"));
            var backInUdapEd = ui.Hook("tiered-code-build-token-request",
                tokenRequestPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Build Access Token Request" }));
            // Case-insensitive: the IdP's consent form names its buttons "Input.button", the authorization server's "Input.Button".
            var allow = page.Locator("button[name='Input.Button' i][value='yes']");
            for (var i = 0; i < 3; i++)
            {
                await allow.Or(backInUdapEd).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
                if (!await allow.IsVisibleAsync())
                {
                    break;
                }

                output.WriteLine($"Allowing consent at {new Uri(page.Url).Host}");
                await allow.ClickAsync();
            }

            await backInUdapEd.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
            Assert.Contains("code=", page.Url);
            output.WriteLine("Returned to UdapEd with an authorization code");

            await ui.ExpandAsync(tokenRequestPanel);
            await backInUdapEd.ClickAsync();
            // The signed client assertion is built by a server round trip; Get Access Token sends it.
            await ui.WaitForTextAsync(tokenRequestPanel, "client_assertion=ey");

            var tokenPanel = ui.Hook("tiered-step-token", ui.PanelTitled("6. Get Access Token"));
            await ui.ExpandAsync(tokenPanel);
            await ui.Hook("tiered-get-access-token",
                tokenPanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Get Access Token" })).ClickAsync();

            await Assertions.Expect(tokenPanel).ToContainTextAsync("access_token", new LocatorAssertionsToContainTextOptions { Timeout = 60_000 });
            var tokenText = await tokenPanel.InnerTextAsync();
            output.WriteLine($"Received tokens for the Tiered OAuth registration. Token panel:\n{tokenText}");
            Assert.Contains("id_token", tokenText);

            // The decoded identity token shows the user came through the upstream IdP, not a local sign-in.
            Assert.Matches("\"idp\":\\s*\"TieredOAuth\"", tokenText);
            Assert.Matches("\"amr\":\\s*\\[\\s*\"external\"", tokenText);
        }
        catch
        {
            await BrowserFixture.SaveTraceAsync(page, nameof(Register_Sign_In_At_IdP_And_Get_Access_Token));
            throw;
        }
    }
}
