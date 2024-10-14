#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using System.Text.Json;
using Udap.CdsHooks.Model;
using UdapEd.Shared.Model.CdsHooks;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class CdsService : ICdsService
{
    private readonly HttpClient _httpClient;

    private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FhirResourceConverter() }
    };

    public CdsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<CdsServiceViewModel>?> FetchCdsServices()
    {
        return _httpClient.GetFromJsonAsync<List<CdsServiceViewModel>> ("CdsServices/FetchCdsServices");
    }

    public async Task<CdsResponse?> GetCdsService(CdsRequest request)
    {
        var response = await  _httpClient.PostAsJsonAsync("CdsServices/GetCdsService", request, jsonOptions);
        var cdsResponses = await response.Content.ReadFromJsonAsync<CdsResponse>();

        return cdsResponses;
    }
}