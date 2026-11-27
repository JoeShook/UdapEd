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
using Microsoft.Extensions.Logging;
using Udap.CdsHooks.Model;
using UdapEd.Shared.Model.CdsHooks;

namespace UdapEd.Shared.Services.Cds;

public class CdsService : ICdsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CdsService> _logger;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
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

    public async Task<List<CdsServiceViewModel>?> FetchCdsServices(string discoveryUrl)
    {
        var discovery = RequireHttpUrl(discoveryUrl, nameof(discoveryUrl));
        var response = await _httpClient.GetAsync(discovery);
        await EnsureSuccess(response, discovery);

        var services = await response.Content.ReadFromJsonAsync<CdsServices>();
        if (services?.Services == null)
        {
            return null;
        }

        // Each service is called at {discovery endpoint}/{id} (CDS Hooks, "Calling a CDS Service").
        var baseUrl = discovery.AbsoluteUri.TrimEnd('/');
        return services.Services
            .Select(service => new CdsServiceViewModel
            {
                Url = $"{baseUrl}/{service.Id}",
                CdsService = new Model.CdsHooks.CdsService(service) { Enabled = true }
            })
            .ToList();
    }

    public async Task<CdsResponse?> GetCdsService(string serviceUrl, CdsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var service = RequireHttpUrl(serviceUrl, nameof(serviceUrl));

        _logger.LogInformation("Calling CDS service {ServiceUrl} for hook {Hook}", service, request.Hook);
        // Sent buffered, with a Content-Length, rather than chunked: simple CDS services may not read chunked bodies.
        using var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(service, content);
        await EnsureSuccess(response, service);

        return await response.Content.ReadFromJsonAsync<CdsResponse>();
    }

    private static Uri RequireHttpUrl(string? value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException($"'{value}' is not an http or https URL.", name);
        }

        return uri;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, Uri url)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // An HTML error page says nothing the status line doesn't; keep only JSON or text bodies.
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var body = mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : await response.Content.ReadAsStringAsync();
        if (body.Length > 500)
        {
            body = body[..500] + "…";
        }

        throw new HttpRequestException(
            $"{url} answered {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim(),
            null,
            response.StatusCode);
    }
}
