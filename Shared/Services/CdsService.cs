#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using UdapEd.Shared.Model.CdsHooks;

namespace UdapEd.Shared.Services;

public class CdsService : ICdsService
{
    private readonly HttpClient _httpClient;
    private string? _defaultSandboxCdsService;

    public CdsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CdsServiceViewModel>?> FetchCdsServices()
    {
        _defaultSandboxCdsService = "https://sandbox-services.cds-hooks.org/cds-services";
        var response = await _httpClient.GetAsync(_defaultSandboxCdsService);
        response.EnsureSuccessStatusCode();
        
        var services = await response.Content.ReadFromJsonAsync<Udap.CdsHooks.Model.CdsServices>();

        var cdsServiceViewModel = new List<CdsServiceViewModel>();

        if (services?.Services != null)
        {
            foreach (var service in services.Services)
            {
                var serviceUrl = $"{_defaultSandboxCdsService}/{service.Id}";
                
                cdsServiceViewModel.Add(new CdsServiceViewModel()
                {
                    Url = serviceUrl,
                    CdsService = new UdapEd.Shared.Model.CdsHooks.CdsService(service)
                });
            }
        }
        else
        {
            return null;
        }

        return cdsServiceViewModel;
    }
}