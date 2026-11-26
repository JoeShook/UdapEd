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
            SlowMo = E2ESettings.SlowMo,
            // When watching, fill the screen and zoom out so whole pages fit (UDAPED_ZOOM, default 67%).
            Args = E2ESettings.Headed
                ? ["--start-maximized", $"--force-device-scale-factor={E2ESettings.Zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}"]
                : []
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
            // Headed: the page follows the maximized window. Headless: a fixed desktop-sized viewport.
            ViewportSize = E2ESettings.Headed ? ViewportSize.NoViewport : new ViewportSize { Width = 1600, Height = 1000 },
            // UDAPED_HAR_DIR=<folder> records every request with its timings, to see where a flow spends its time.
            RecordHarPath = Environment.GetEnvironmentVariable("UDAPED_HAR_DIR") is { Length: > 0 } harDir
                ? Path.Combine(harDir, $"udaped-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.har")
                : null
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
            // Closing each context writes its HAR file when UDAPED_HAR_DIR is set.
            var storageDir = Environment.GetEnvironmentVariable("UDAPED_STORAGE_DIR");
            foreach (var context in Browser.Contexts.ToList())
            {
                // UDAPED_STORAGE_DIR=<folder> keeps each test's final cookies and localStorage (UdapEd's app state),
                // for reproducing a realistic state when profiling.
                if (!string.IsNullOrEmpty(storageDir))
                {
                    Directory.CreateDirectory(storageDir);
                    await context.StorageStateAsync(new BrowserContextStorageStateOptions
                    {
                        Path = Path.Combine(storageDir, $"udaped-state-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json")
                    });
                }

                await context.CloseAsync();
            }

            await Browser.DisposeAsync();
            _playwright.Dispose();
        }
    }
}
