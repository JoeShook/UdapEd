using System.Net.Http.Json;
using UdapEd.Shared.Model.CdsHooks;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class CdsService : ICdsService
{
    private readonly HttpClient _httpClient;

    public CdsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<CdsServiceViewModel>?> FetchCdsServices()
    {
        return _httpClient.GetFromJsonAsync<List<CdsServiceViewModel>> ("CdsServices/FetchCdsServices");
    }
}