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

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FhirResourceConverter() }
    };

    public CdsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CdsServiceViewModel>?> FetchCdsServices(string discoveryUrl)
    {
        var response = await _httpClient.GetAsync($"CdsServices/FetchCdsServices?discoveryUrl={Uri.EscapeDataString(discoveryUrl)}");
        await EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<List<CdsServiceViewModel>>(JsonOptions);
    }

    public async Task<CdsResponse?> GetCdsService(string serviceUrl, CdsRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"CdsServices/GetCdsService?serviceUrl={Uri.EscapeDataString(serviceUrl)}", request, JsonOptions);
        await EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<CdsResponse>(JsonOptions);
    }

    // The server answers a failed call with the reason as plain text; surface it to the page.
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var reason = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(reason) ? $"{(int)response.StatusCode} {response.ReasonPhrase}" : reason,
                null,
                response.StatusCode);
        }
    }
}
