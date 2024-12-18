﻿using Microsoft.UI.Xaml;
using Microsoft.Maui.Authentication;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UdapEdAppMaui.WinUI;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        //
        // See: https://dotmorten.github.io/WinUIEx/concepts/Maui.html#use-winuiexs-webauthenticator-instead-of-net-mauis
        //
        if (WinUIEx.WebAuthenticator.CheckOAuthRedirectionActivation())
            return;

        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

