#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Text.Json.Serialization;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Udap.CdsHooks.Model;
using Udap.Client.Client;
using Udap.Client.Configuration;
using Udap.Common.Certificates;
using UdapEd.Server.Extensions;
using UdapEd.Server.Rest;
using UdapEd.Server.Services.Authentication;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Authentication;
using UdapEd.Shared.Services.Cds;
using CdsService = UdapEd.Shared.Services.Cds.CdsService;
using FhirClientWithUrlProvider = UdapEd.Shared.Services.FhirClientWithUrlProvider;
using IBaseUrlProvider = UdapEd.Shared.Services.IBaseUrlProvider;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
    .MinimumLevel.Override("IdentityModel", LogEventLevel.Debug)
    .MinimumLevel.Override("Duende.Bff", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
        theme: AnsiConsoleTheme.Code));

// Mount Cloud Secrets
builder.Configuration.AddJsonFile("/secret/udapEdAppsettings", true, false);

builder.Services.AddSingleton<CrlCacheService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = ".FhirLabs.UdapEd";
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews(options =>
{
    // options.Filters.Add(new UserPreferenceFilter());
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new FhirResourceConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddRazorPages();
// builder.Services.AddBff();

//
// builder.Services.AddAuthentication(options =>
//     {
//         options.DefaultScheme = "cookie";
//         options.DefaultChallengeScheme = "oidc";
//         options.DefaultSignOutScheme = "oidc";
//     })
//     .AddCookie("cookie", options =>
//     {
//         options.Cookie.Name = "__UdapClientBackend";
//         options.Cookie.SameSite = SameSiteMode.Strict;
//     })
//     .AddOpenIdConnect("oidc", options =>
//     {
//         options.Authority = "https://loclahost:5002";
//
//         // Udap Authorization code flow
//         options.ClientId = "interactive.confidential";  //TODO Dynamic
//         options.ClientSecret = "secret";
//         options.ResponseType = "code";
//         options.ResponseMode = "query";
//
//         options.MapInboundClaims = false;
//         options.GetClaimsFromUserInfoEndpoint = true;
//         options.SaveTokens = true;
//
//         // request scopes + refresh tokens
//         options.Scope.Clear();
//         options.Scope.Add("openid");
//         options.Scope.Add("profile");
//         options.Scope.Add("api");
//         options.Scope.Add("offline_access");
//
//     });

builder.Services.AddScoped<TrustChainValidator>();
builder.Services.AddScoped<UdapClientDiscoveryValidator>();
builder.Services.AddHttpClient<IUdapClient, UdapClient>()
    .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()));

builder.Services.AddScoped<IBaseUrlProvider, BaseUrlProvider>();
builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();

builder.Services.AddHttpClient<FhirClientWithUrlProvider>((sp, httpClient) =>
{ })
    .AddHttpMessageHandler(sp => new AuthTokenHttpMessageHandler(sp.GetRequiredService<IAccessTokenProvider>()))
    .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()));

builder.Services.AddTransient<IClientCertificateProvider, ClientCertificateProvider>();
builder.Services.AddScoped<IInfrastructure, Infrastructure>();
builder.Services.AddHttpClient<ICdsService, CdsService>();
builder.Services.AddHttpClient<IServiceExchange, ServiceExchange>();


//
// This does not allow you to let the client dynamically load new mTLS certificates.  The HttpHandler doesn't reenter.
//

// builder.Services.AddHttpClient<FhirMTlsClientWithUrlProvider>((sp, httpClient) =>
//     {})
//     .AddHttpMessageHandler(sp => new HeaderAugmentationHandler(sp.GetRequiredService<IOptionsMonitor<UdapClientOptions>>()))
//     .ConfigurePrimaryHttpMessageHandler((sp) =>
//     {
//         var certificateProvider = sp.GetRequiredService<IClientCertificateProvider>();
//         var httpClientHandler = new HttpClientHandler();
//         var certificate = certificateProvider.GetClientCertificate(default);
//         if (certificate != null)
//         {
//             Log.Logger.Information($"mTLS Client: {certificate.Thumbprint}");
//             httpClientHandler.ClientCertificates.Add(certificate);
//         }
//         return httpClientHandler;
//     });

//
// Could take some effort to implement a pool based on thumbprint of the client certificate,
// but we won't exhaust ports when it is just a client tool.  
// I could cache Handlers.  After all there won't be a DNS issue because the IPs for this sort
// of mTLS relationship is typically static.
//
//
builder.Services.AddTransient<FhirMTlsClientWithUrlProvider>(sp =>
{
    var baeUrlProvider = sp.GetRequiredService<IBaseUrlProvider>();
    var httpClientHandler = new HttpClientHandler();
    var certificateProvider = sp.GetRequiredService<IClientCertificateProvider>();
    var certificate = certificateProvider.GetClientCertificate();
    var anchorCertificate = certificateProvider.GetAnchorCertificates();
    // var intermediateCertificate = new X509Certificate2Collection(new X509Certificate2("SureFhirmTLS_Intermediate.cer"));
    
    if (certificate != null)
    {
        Log.Logger.Information($"mTLS Client: {certificate.Thumbprint}");
        httpClientHandler.ClientCertificates.Add(certificate);
    }

    if (anchorCertificate != null)
    {
        // httpClientHandler.CheckCertificateRevocationList = true;
        // httpClientHandler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        var logger = sp.GetRequiredService<ILogger<Program>>();
        httpClientHandler.ServerCertificateCustomValidationCallback =
            HttpClientHandlerExtension.CreateCustomRootValidator(anchorCertificate, logger);
    }
    
    var fhirMTlsProvider = new FhirMTlsClientWithUrlProvider(
        baeUrlProvider, 
        new HttpClient(httpClientHandler), 
        new FhirClientSettings(){PreferredFormat = ResourceFormat.Json});

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



builder.Services.AddHttpContextAccessor();

builder.AddRateLimiting();

// Configure OpenTelemetry
builder.AddOpenTelemetry();

var app = builder.Build();

app.UseSerilogRequestLogging();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter(); //after routing

app.UseSession();
app.MapRazorPages();
app.MapControllers().RequireRateLimiting(RateLimitExtensions.Policy);

app.MapFallbackToFile("index.html");


//
// Created to route traffic through AEGIS Touchstone via a Nginx reverse proxy in my cloud environment.
// Touchstone is also a proxy used to surveil traffic for testing and certification.  
//
if (Environment.GetEnvironmentVariable("proxy-hosts") != null)
{
    var hostMaps = Environment.GetEnvironmentVariable("proxy-hosts")?.Split(";");
    foreach (var hostMap in hostMaps!)
    {
        Log.Information($"Adding host map: {hostMap}");
        File.AppendAllText("/etc/hosts", hostMap + Environment.NewLine);
    }
}

app.Run();
