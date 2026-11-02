#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

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
        await Page.GotoAsync(path);
        await ByTestId(readyTestId).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 90_000 });
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

    /// <summary>Picks an option in a MudSelect by its visible text.</summary>
    public async Task SelectAsync(string selectTestId, string optionText)
    {
        await ByTestId(selectTestId).First.ClickAsync();
        var option = Page.Locator(".mud-popover-open .mud-list-item").Filter(new LocatorFilterOptions { HasText = optionText }).First;
        await option.ClickAsync();
        await Page.Locator(".mud-popover-open").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    /// <summary>Waits until an element's text contains the given value and returns the text.</summary>
    public async Task<string> WaitForTextAsync(string testId, string contains, float timeout = 60_000)
    {
        var locator = ByTestId(testId).First;
        await Assertions.Expect(locator).ToContainTextAsync(contains, new LocatorAssertionsToContainTextOptions { Timeout = timeout });
        return await locator.InnerTextAsync();
    }
}
