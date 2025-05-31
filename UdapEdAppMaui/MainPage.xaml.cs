using Microsoft.Maui.Controls.PlatformConfiguration;

#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UIKit;
#endif

using UdapEd.Shared.Services;

#if WINDOWS
using UdapEdAppMaui.Services;
using WinUIEx;
#endif

namespace UdapEdAppMaui;


public partial class MainPage : ContentPage, IMainPageService
{
#if IOS
    
    private readonly IDeviceOrientationService _deviceOrientationService;
    
    public MainPage(IDeviceOrientationService deviceOrientationService)
    {
        _deviceOrientationService = deviceOrientationService;
        InitializeComponent();

        this.SizeChanged += OnSizeChanged;
        SetSafeAreaPadding();

        DependencyService.RegisterSingleton<IMainPageService>(this);
    }
#else
    public MainPage()
    {
        InitializeComponent();
        DependencyService.RegisterSingleton<IMainPageService>(this);
    }
#endif
    
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
#if IOS
        SetSafeAreaPadding();
#endif
    }

#if IOS
    void OnSizeChanged(object? sender, EventArgs e)
    {
        SetSafeAreaPadding();
    }

    void SetSafeAreaPadding()
    {
        var insets = On<iOS>().SafeAreaInsets();
        var orientation = _deviceOrientationService.GetOrientation();

        switch (orientation)
        {
            case UIDeviceOrientation.LandscapeLeft:
                this.Padding = new Thickness(insets.Left, 0, 0, 0);
                break;
            case UIDeviceOrientation.LandscapeRight:
                this.Padding = new Thickness(0, 0, insets.Right, 0);
                break;
            case UIDeviceOrientation.Portrait:
            case UIDeviceOrientation.PortraitUpsideDown:
                this.Padding = new Thickness(0, insets.Top, 0, 0);
                break;
            default:
                this.Padding = new Thickness(0);
                break;
        }
    }
#endif

    public void BringToFront()
    {
#if WINDOWS
        var window = this.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window != null) window.SetForegroundWindow();
#endif
    }
}