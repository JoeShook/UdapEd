#if WINDOWS
using WinUIEx;
#endif
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;

public sealed class MainPageService : IMainPageService
{
    public void BringToFront()
    {
#if WINDOWS
        var mauiWindow = Application.Current?.Windows.FirstOrDefault();
        var window = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        window?.SetForegroundWindow();
#endif
        // No-op on other platforms
    }
}