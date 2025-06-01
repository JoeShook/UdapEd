#region (c) 2024 Joseph Shook. All rights reserved.
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
using CommunityToolkit.Maui;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Udap.Client.Client;
using Udap.Client.Configuration;
using Udap.Common.Certificates;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Authentication;
using UdapEd.Shared.Services.Cds;
using UdapEd.Shared.Services.Fhir;
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
using UdapEdAppMaui.Services.Fhir;
using UdapEdAppMaui.Services.Search;
using AuthTokenHttpMessageHandler = UdapEd.Shared.Services.Authentication.AuthTokenHttpMessageHandler;
using IAccessTokenProvider = UdapEd.Shared.Services.Authentication.IAccessTokenProvider;
using Infrastructure = UdapEdAppMaui.Services.Infrastructure;



namespace UdapEdAppMaui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiCommunityToolkit();
        var flushInterval = new TimeSpan(0, 0, 1);
        
        #if MACCATALYST
            var file = Path.Combine(FileSystem.AppDataDirectory, "Application Support", "UdapEdAppMaui.log");    
        #else
            var file = Path.Combine(FileSystem.AppDataDirectory, "UdapEdAppMaui.log");
        #endif
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

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });
        
#if MACCATALYST || ANDROID
    using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").Result;
    var config = new ConfigurationBuilder()
        .AddJsonStream(stream)
        .Build();
    builder.Configuration.AddConfiguration(config);
#endif
#if IOS
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("UdapEdAppMaui.appsettings.json");
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
        builder.Configuration.AddConfiguration(config);
#endif
#if WINDOWS
            // Just so I can use the secrets.json on Windows.  Convenient for loading EMR Direct cert quickly.
            

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
        builder.Services.AddSingleton<CrlCacheService>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped(sp => new HttpClient());
        builder.Services.AddMudServices();
        builder.Services.AddBlazoredLocalStorage();

        builder.Services.AddSingleton<AppSharedState>();
        builder.Services.AddSingleton<UdapClientState>(); //Singleton in Blazor wasm and Scoped in Blazor Server
        builder.Services.AddScoped<IRegisterService, RegisterService>();
        builder.Services.AddScoped<ICertificationService, CertificationService>();
        builder.Services.AddScoped<IAccessService, AccessService>();
        builder.Services.AddTransient<IFhirService, FhirService>();

        
        // builder.Services.AddScoped<IInfrastructure, Infrastructure>();

        builder.Services.AddHttpClient("UdapEdServer", client =>
        {
            client.BaseAddress = new Uri("https://udaped.fhirlabs.net/");
        });

        builder.Services.AddScoped<IInfrastructure>(sp =>
            new UdapEdAppMaui.Services.IOS.Infrastructure(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("UdapEdServer"),
                sp.GetRequiredService<ILogger<UdapEdAppMaui.Services.IOS.Infrastructure>>()
            )
        );

        builder.Services.AddScoped<IDiscoveryService>(sp =>
            new UdapEdAppMaui.Services.IOS.DiscoveryService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("UdapEdServer"),
                sp.GetRequiredService<ILogger<UdapEdAppMaui.Services.IOS.DiscoveryService>>()
            )
        );

#if IOS
        builder.Services.AddSingleton<IDeviceOrientationService, DeviceOrientationService>();
#endif

        builder.Services.AddHttpClient<ICdsService, CdsService>();
        builder.Services.AddHttpClient<IServiceExchange, ServiceExchange>();

        builder.Services.AddTransient<TrustChainValidator>();
        builder.Services.AddTransient<UdapClientDiscoveryValidator>();
        builder.Services.AddHttpClient<IUdapClient, UdapClient>()
            .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()));
        builder.Services.AddSingleton<ICapabilityLookup, CapabilityLookup>();
        builder.Services.AddScoped<IClipboardService, ClipboardService>();
        builder.Services.AddScoped<IMutualTlsService, MutualTlsService>();

        builder.Services.AddTransient<IBaseUrlProvider, BaseUrlProvider>();
        builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();
        builder.Services.AddSingleton<IFhirClientOptionsProvider, FhirClientOptionsProvider>();
        builder.Services.AddTransient<HttpResponseHandler>();

        builder.Services.AddHttpClient<FhirClientWithUrlProvider>((sp, httpClient) =>
                { })
            .AddHttpMessageHandler(sp => new AuthTokenHttpMessageHandler(sp.GetRequiredService<IAccessTokenProvider>()))
            .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()))
            .AddHttpMessageHandler(sp => new CustomDecompressionHandler(sp.GetRequiredService<IFhirClientOptionsProvider>()))
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
                var logger = sp.GetRequiredService<ILogger<HttpClientHandler>>();
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandlerExtension.CreateCustomRootValidator(anchorCertificate, logger);
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




        return builder.Build();
    }
}
