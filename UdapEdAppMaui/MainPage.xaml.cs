
using UdapEd.Shared.Services;
#if WINDOWS
using UdapEdAppMaui.Services;
using WinUIEx;
#endif

namespace UdapEdAppMaui;


public partial class MainPage : TabbedPage, IMainPageService
{
    public MainPage()
    {
        InitializeComponent();
        DependencyService.RegisterSingleton<IMainPageService>(this);
    }

    private void OnMaximizeClicked(object sender, EventArgs e)
    {

    }


    private void OnFullScreenClicked(object sender, EventArgs e)
    {

    }

    public void BringToFront()
    {
#if WINDOWS
        var window = this.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window != null) window.SetForegroundWindow();
#endif
    }
}
