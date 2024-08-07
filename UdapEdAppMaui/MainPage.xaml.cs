﻿namespace UdapEdAppMaui;
#if WINDOWS
using WinUIEx;
#endif

public partial class MainPage : TabbedPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnMaximizeClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var window = this.Window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
        window.Maximize(); // Use WinUIEx Extension method to maximize window
#endif
    }


    private void OnFullScreenClicked(object sender, EventArgs e)
    {
#if WINDOWS
        // Get the window manager
        var manager = WinUIEx.WindowManager.Get(this.Window.Handler.PlatformView as Microsoft.UI.Xaml.Window);
        if (manager.PresenterKind == Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped)
            manager.PresenterKind = Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
        else
            manager.PresenterKind = Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped;
#endif
    }
}
