using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UdapEd.Shared.Services;
using TabbedPage = Microsoft.Maui.Controls.TabbedPage;
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

    protected override void OnAppearing()
    {
#if IOS
        // Use the safe area insets if available
        var insets = this.On<iOS>().SafeAreaInsets();
        this.Padding = this.On<iOS>().SafeAreaInsets();        
#endif
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
