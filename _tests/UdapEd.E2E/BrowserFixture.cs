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
/// One Chromium instance shared by the tests in a class. Each test gets a fresh browser context,
/// so UdapEd's local storage (certificates, registrations) never carries over between tests.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (E2ESettings.UdapEdUrl == null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !E2ESettings.Headed,
            SlowMo = E2ESettings.SlowMo
        });
    }

    /// <summary>
    /// A new context and page for one test. Tracing is on; <see cref="SaveTraceAsync"/> keeps it when the test fails.
    /// </summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2ESettings.UdapEdUrl,
            // A local UdapEd serves the ASP.NET development certificate.
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });

        context.SetDefaultTimeout(30_000);
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        return await context.NewPageAsync();
    }

    /// <summary>Writes the Playwright trace for a failed test to TestResults/traces.</summary>
    public static async Task SaveTraceAsync(IPage page, string testName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "traces");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{testName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

        await page.Context.Tracing.StopAsync(new TracingStopOptions { Path = path });
        Console.WriteLine($"Playwright trace: {path} (open with: pwsh bin/Debug/net10.0/playwright.ps1 show-trace \"{path}\")");
    }

    public async Task DisposeAsync()
    {
        if (_playwright != null)
        {
            await Browser.DisposeAsync();
            _playwright.Dispose();
        }
    }
}
