﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Reflection;
using Blazored.LocalStorage;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Udap.Client.Authentication;
using Udap.Client.Client;
using Udap.Client.Configuration;
using Udap.Common.Certificates;
using UdapEd.Shared.Pages;
using UdapEd.Shared.Search;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Authentication;
using UdapEd.Shared.Services.Search;
/* Unmerged change from project 'UdapEdAppMaui (net8.0-windows10.0.19041.0)'
Before:
using UdapEdAppMaui.Services;


#if WINDOWS
After:
using UdapEdAppMaui.Services;
using UdapEdAppMaui.Services.Authentication;



#if WINDOWS
*/
using UdapEdAppMaui.Services;
using UdapEdAppMaui.Services.Authentication;
using UdapEdAppMaui.Services.Search;


#if WINDOWS
using WinUIEx;
#endif

namespace UdapEdAppMaui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        var flushInterval = new TimeSpan(0, 0, 1);
        var file = Path.Combine(FileSystem.AppDataDirectory, "UdapEdAppMaui.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft.AspNetCore.Components.RenderTree.Renderer", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Components.WebView", LogEventLevel.Verbose)
            .Enrich.FromLogContext()
            // .WriteTo.Console(
            //     outputTemplate:
            //     "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
            //     theme: AnsiConsoleTheme.Code)
#if ANDROID
            .WriteTo.AndroidLog()
#endif
            .WriteTo.File(file, 
                flushToDiskInterval: flushInterval,
                encoding: System.Text.Encoding.UTF8, 
                rollingInterval: RollingInterval.Day, 
                retainedFileCountLimit: 22,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Logging.AddSerilog(dispose: true);

        builder.Services.AddLogging(logging => { logging.AddDebug(); });

#if WINDOWS
            // Just so I can use the secrets.json on Windows.  Convenient for loading EMR Direct cert quickly.
            builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var a = Assembly.GetExecutingAssembly();
        using var stream = a.GetManifestResourceStream("UdapEdAppMaui.appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonStream(stream)
            .AddUserSecrets<RegisterService>() 
            .Build();

        builder.Configuration.AddConfiguration(configuration);

        // Register services
        builder.Services.AddSingleton<IConfiguration>(configuration);
#endif

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped(sp => new HttpClient());
        builder.Services.AddMudServices();
        builder.Services.AddBlazoredLocalStorage();

        builder.Services.AddSingleton<AppSharedState>();
        builder.Services.AddSingleton<UdapClientState>(); //Singleton in Blazor wasm and Scoped in Blazor Server
        builder.Services.AddSingleton<IRegisterService, RegisterService>();
        builder.Services.AddSingleton<IDiscoveryService, DiscoveryService>();
        builder.Services.AddSingleton<IAccessService, AccessService>();
        builder.Services.AddSingleton<IFhirService, FhirService>();
        builder.Services.AddSingleton<IInfrastructure, Infrastructure>();


        builder.Services.AddSingleton<TrustChainValidator>();
        builder.Services.AddSingleton<UdapClientDiscoveryValidator>();
        builder.Services.AddHttpClient<IUdapClient, UdapClient>()
            .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()));
        builder.Services.AddSingleton<ICapabilityLookup, CapabilityLookup>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<IMutualTlsService, MutualTlsService>();

        builder.Services.AddScoped<IBaseUrlProvider, BaseUrlProvider>();
        builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();

        builder.Services.AddTransient<PatientSearch>(); //Weird
        builder.Services.AddTransient<HttpResponseHandler>();

        builder.Services.AddHttpClient<FhirClientWithUrlProvider>((sp, httpClient) =>
                { })
            .AddHttpMessageHandler(sp => new AuthTokenHttpMessageHandler(sp.GetRequiredService<IAccessTokenProvider>()))
            .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()))
            .AddHttpMessageHandler<HttpResponseHandler>();

        builder.Services.AddTransient<IClientCertificateProvider, ClientCertificateProvider>();

        builder.Services.AddTransient<FhirMTlsClientWithUrlProvider>(sp =>
        {
            var baeUrlProvider = sp.GetRequiredService<IBaseUrlProvider>();
            var httpClientHandler = new System.Net.Http.HttpClientHandler();
            var certificateProvider = sp.GetRequiredService<IClientCertificateProvider>();
            var certificate = certificateProvider.GetClientCertificate();
            var anchorCertificate = certificateProvider.GetAnchorCertificates();

            if (certificate != null)
            {
                Log.Logger.Information($"mTLS Client: {certificate.Thumbprint}");
                httpClientHandler.ClientCertificates.Add(certificate);
            }

            if (anchorCertificate != null)
            {
                // httpClientHandler.CheckCertificateRevocationList = true;
                // httpClientHandler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandlerExtension.CreateCustomRootValidator(anchorCertificate);
            }

            var fhirMTlsProvider = new FhirMTlsClientWithUrlProvider(
                baeUrlProvider,
                new HttpClient(httpClientHandler),
                new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });

            return fhirMTlsProvider;
        });

        var url = builder.Configuration["FHIR_TERMINOLOGY_ROOT_URL"];

        var settings = new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json,
            VerifyFhirVersion = false
        };


        builder.Services.AddHttpClient("FhirTerminologyClient")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });

        builder.Services.AddTransient(ctx => {

            var httpClient = ctx.GetRequiredService<IHttpClientFactory>().CreateClient("FhirTerminologyClient");
            return new FhirClient(url, httpClient, settings);
        });



#if WINDOWS
        builder.Services.AddSingleton<IExternalWebAuthenticator, WebAuthenticatorForWindows>();
#else
        builder.Services.AddSingleton<IExternalWebAuthenticator, WebAuthenticatorForDevice>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

#if WINDOWS
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(wndLifeCycleBuilder =>
                {
                    wndLifeCycleBuilder.OnWindowCreated(window =>
                    {
                        window.CenterOnScreen(1024,768); //Set size and center on screen using WinUIEx extension method

                        var manager = WinUIEx.WindowManager.Get(window);
                        manager.PersistenceId = "MainWindowPersistanceId"; // Remember window position and size across runs
                        manager.MinWidth = 640;
                        manager.MinHeight = 480;
                    });
                });
            });
#endif


        return builder.Build();
    }
}
