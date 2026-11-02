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

namespace UdapEd.E2E;

/// <summary>
/// Helpers for driving UdapEd's MudBlazor UI by its data-testid hooks.
/// </summary>
public sealed class UdapEdPage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator ByTestId(string testId) => Page.Locator($"[data-testid='{testId}']");

    /// <summary>The input element of a MudBlazor field, whether the test id lands on the input or its wrapper.</summary>
    public ILocator InputOf(string testId) =>
        Page.Locator($"input[data-testid='{testId}'], [data-testid='{testId}'] input").First;

    /// <summary>Loads UdapEd at a page and waits for the WebAssembly app to render it.</summary>
    public async Task OpenAsync(string path, string readyTestId)
    {
        // The WebAssembly download can be slow (cold start, slow network): wait for the app to render,
        // not for every resource's load event.
        await Page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 90_000 });
        await ByTestId(readyTestId).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 120_000 });
    }

    /// <summary>
    /// Moves to another page through the navigation menu, so the Blazor app (and its state) stays loaded.
    /// </summary>
    public async Task NavigateAsync(string path, string readyTestId)
    {
        var link = Page.Locator($"a[href='{path}'], a[href='/{path}']").First;
        if (await link.CountAsync() > 0 && await link.IsVisibleAsync())
        {
            await link.ClickAsync();
        }
        else
        {
            await Page.EvaluateAsync("path => Blazor.navigateTo(path)", path);
        }

        await ByTestId(readyTestId).First.WaitForAsync();
    }

    /// <summary>Opens a MudExpansionPanel unless it is already expanded.</summary>
    public async Task ExpandAsync(string panelTestId)
    {
        var panel = ByTestId(panelTestId);
        await panel.WaitForAsync();
        var classes = await panel.GetAttributeAsync("class") ?? string.Empty;
        if (!classes.Contains("mud-panel-expanded"))
        {
            await panel.Locator(".mud-expand-panel-header").First.ClickAsync();
        }
    }

    /// <summary>Ticks a MudCheckBox unless it is already checked.</summary>
    public async Task CheckAsync(string checkBoxTestId)
    {
        var input = Page.Locator($"input[type='checkbox'][data-testid='{checkBoxTestId}'], [data-testid='{checkBoxTestId}'] input[type='checkbox']").First;
        if (!await input.IsCheckedAsync())
        {
            await input.CheckAsync(new LocatorCheckOptions { Force = true });
        }
        await Assertions.Expect(input).ToBeCheckedAsync();
    }

    /// <summary>Picks an option in a MudSelect by its visible text.</summary>
    public async Task SelectAsync(string selectTestId, string optionText)
    {
        await ByTestId(selectTestId).First.ClickAsync();
        var option = Page.Locator(".mud-popover-open .mud-list-item").Filter(new LocatorFilterOptions { HasText = optionText }).First;
        await option.ClickAsync();
        await Page.Locator(".mud-popover-open").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    /// <summary>Opens UdapEd on the discovery page and queries the FHIR server's UDAP metadata for the configured community.</summary>
    public async Task DiscoverAsync()
    {
        await OpenAsync("/udapDiscovery", "discovery-query");
        await InputOf("discovery-base-url").FillAsync(E2ESettings.FhirBaseUrl);
        await InputOf("discovery-community").FillAsync(E2ESettings.Community);
        await ByTestId("discovery-query").ClickAsync();
        await WaitForTextAsync("discovery-result", "registration_endpoint");
    }

    /// <summary>
    /// Loads a client certificate the way a user does: the Upload button, then the password dialog.
    /// Works on any page that shows the certificate loader.
    /// </summary>
    public async Task UploadCertificateAsync(string path, string password)
    {
        await Page.Locator("#fileInput").SetInputFilesAsync(path);
        await InputOf("cert-password").FillAsync(password);
        await ByTestId("cert-password-ok").ClickAsync();
        await ByTestId("cert-thumbprint").WaitForAsync();
    }

    /// <summary>
    /// Registers a client on the registration page and returns its client_id: uploads the certificate, picks the
    /// flow, subject alt name and RS256, builds the software statement and request body, and runs DCR.
    /// <paramref name="chooseScopes"/> runs inside the expanded "Build Software Statements" panel before the build.
    /// </summary>
    public async Task<string> RegisterAsync(string certificatePath, string flow, string subjectAltName, Func<Task>? chooseScopes = null)
    {
        await NavigateAsync("udapRegistration", "cert-prepackaged-menu");
        await UploadCertificateAsync(certificatePath, E2ESettings.CertificatePassword);

        await SelectAsync("registration-flow", flow);
        await SelectAsync("registration-san", subjectAltName);
        await SelectAsync("registration-alg", "RS256");

        await ExpandAsync("registration-step-statement");
        if (chooseScopes != null)
        {
            await chooseScopes();
        }

        await ByTestId("registration-build").ClickAsync();
        // Build Register fills the software statement asynchronously; the next step posts it.
        await Assertions.Expect(Page.Locator("#softwareStatementTextarea"))
            .ToHaveValueAsync(new Regex(Regex.Escape(subjectAltName)), new LocatorAssertionsToHaveValueOptions { Timeout = 30_000 });
        await Assertions.Expect(ByTestId("registration-statement-error")).ToHaveCountAsync(0);

        await ExpandAsync("registration-step-body");
        await ByTestId("registration-build-body").ClickAsync();
        // The signed request body is built by a server round trip; Register posts it. Wait for the generated
        // JSON key: the panel's static labels also say "software_statement" (without quotes).
        await WaitForTextAsync("registration-step-body", "\"software_statement\"");

        await ExpandAsync("registration-step-register");
        await ByTestId("registration-register").ClickAsync();
        var registration = await WaitForTextAsync("registration-result", "client_id");
        var clientId = Regex.Match(registration, "\"client_id\"\\s*:\\s*\"(?<id>[^\"]+)\"").Groups["id"].Value;
        Assert.False(string.IsNullOrEmpty(clientId), $"No client_id in the registration response:\n{registration}");

        return clientId;
    }

    /// <summary>
    /// Signs in on an authorization server or IdP sign-in page with its first test account. Handles both page styles:
    /// the SecuredControls one-click account buttons, and Udap.UI's fill-in buttons followed by Login.
    /// </summary>
    public async Task SignInWithTestAccountAsync()
    {
        var oneClick = Page.Locator("button.sc-account");
        var fillIn = Page.Locator("[data-test-account='true']");
        await oneClick.Or(fillIn).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });

        if (await oneClick.CountAsync() > 0)
        {
            await oneClick.First.ClickAsync();
        }
        else
        {
            await fillIn.First.ClickAsync();
            await Page.Locator("button[name='Input.Button'][value='login']").First.ClickAsync();
        }
    }

    /// <summary>Requests the token on the B2B page's final step and returns the response text; fails on a token error.</summary>
    public async Task<string> GetAccessTokenAsync()
    {
        await ExpandAsync("b2b-step-token");
        await ByTestId("b2b-get-access-token").ClickAsync();

        var tokenOrError = Page.Locator("[data-testid='b2b-access-token']:has-text('access_token'), [data-testid='b2b-token-error']").First;
        await tokenOrError.WaitForAsync(new LocatorWaitForOptions { Timeout = 60_000 });
        if (await ByTestId("b2b-token-error").CountAsync() > 0)
        {
            Assert.Fail($"Token request failed:\n{await ByTestId("b2b-token-error").InnerTextAsync()}");
        }

        return await ByTestId("b2b-access-token").InnerTextAsync();
    }

    /// <summary>Waits until an element's text contains the given value and returns the text.</summary>
    public async Task<string> WaitForTextAsync(string testId, string contains, float timeout = 60_000)
    {
        var locator = ByTestId(testId).First;
        await Assertions.Expect(locator).ToContainTextAsync(contains, new LocatorAssertionsToContainTextOptions { Timeout = timeout });
        return await locator.InnerTextAsync();
    }
}
