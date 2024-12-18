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
using System.Text.Json.Serialization;
using Google.Apis.Logging;
using Microsoft.Extensions.Logging;
using Udap.CdsHooks.Model;
using UdapEd.Shared.Model.CdsHooks;

namespace UdapEd.Shared.Services.Cds;

public class CdsService : ICdsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CdsService> _logger;
    private string? _defaultSandboxCdsService = "https://sandbox-services.cds-hooks.org/cds-services";
    private static JsonSerializerOptions  jsonSerializerOptions = new JsonSerializerOptions
    {
        Converters = { new FhirResourceConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CdsService(HttpClient httpClient, ILogger<CdsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<CdsServiceViewModel>?> FetchCdsServices()
    {
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
                    CdsService = new Model.CdsHooks.CdsService(service) { Enabled = true }
                });
            }
        }
        else
        {
            return null;
        }

        return cdsServiceViewModel;
    }

    public async Task<CdsResponse?> GetCdsService(CdsRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var serviceUrl = "https://sandbox-services.cds-hooks.org/cds-services/patient-greeting";
        _logger.LogInformation($"Hello::{serviceUrl}");
        var response = await _httpClient.PostAsJsonAsync(serviceUrl, request, jsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        var cdsResponses = await response.Content.ReadFromJsonAsync<CdsResponse>();
        _logger.LogInformation("Joe");
        _logger.LogInformation(cdsResponses.Cards.First().Summary);
        return cdsResponses;
    }
}