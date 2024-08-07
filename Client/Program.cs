#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Blazored.LocalStorage;
using BQuery;
using Hl7.Fhir.FhirPath;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using UdapEd.Client.Services;
using UdapEd.Client.Services.Search;
using UdapEd.Shared;
using UdapEd.Shared.Search;
using UdapEd.Shared.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");


builder.Services.AddSingleton(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddSingleton<AppSharedState>();
builder.Services.AddScoped<IRegisterService, RegisterService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<IFhirService, FhirService>();
builder.Services.AddScoped<IInfrastructure, Infrastructure>();
builder.Services.AddSingleton<CapabilityLookup>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<IMutualTlsService, MutualTlsService>();

// Add this so that the resolve() extension will be available when including in FhirPath
Hl7.FhirPath.FhirPathCompiler.DefaultSymbolTable.AddFhirExtensions();

await builder.Build().UseBQuery().RunAsync();