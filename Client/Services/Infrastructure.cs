#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class Infrastructure : IInfrastructure
{
    private HttpClient _httpClient;
    private readonly ILogger<Infrastructure> _logger;

    public Infrastructure(HttpClient httpClient, ILogger<Infrastructure> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetMyIp()
    {
       var response = await _httpClient.GetAsync("Metadata/MyIp");

       if (response.IsSuccessStatusCode)
       {
           return await response.Content.ReadAsStringAsync();
       }

       return string.Empty;
    }

    public async Task<byte[]> BuildMyTestCertificatePackage(List<string> subjAltNames)
    {
        var queryString = string.Join("&", subjAltNames.Select(name => $"subjAltNames={Uri.EscapeDataString(name)}"));
        var response = await _httpClient.GetAsync($"Infrastructure/BuildMyTestCertificatePackage?{queryString}");

        if (response.IsSuccessStatusCode)
        {
            var contentBase64 = await response.Content.ReadAsStringAsync();
            return Convert.FromBase64String(contentBase64);
        }

        return new byte[] { };
    }

    /// <summary>
    /// Not implemented.  This is a web service to allow udap client certificates from the Fhirlabs community in automation workflows.
    /// Maybe something like HL7-FAST Foundry can use this to generate a client certificate for a UDAP Tiered enabled Authorization server.
    /// The implementation will only be exposed on the Web Service and not in the client UI.
    /// </summary>
    /// <param name="subjAltNames"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task<byte[]> JitFhirlabsCommunityCertificate(List<string> subjAltNames, string password)
    {
        throw new NotImplementedException();
    }

    public async Task<CertificateViewModel?> GetX509data(string url)
    {
        try{
            var response = await _httpClient.GetAsync($"Infrastructure/GetX509data?url={url}");

            return await response.Content.ReadFromJsonAsync<CertificateViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GetCertificateData from list");
            return null;
        }
    }

    public async Task<string?> GetCrldata(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync($"Infrastructure/GetCrldata?url={url}");

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GetCertificateData from list");
            return null;
        }
    }

    public async Task<X509CacheSettings?> GetX509StoreCache(string thumbprint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"Infrastructure/GetX509StoreCache?thumbprint={thumbprint}");

            return await response.Content.ReadFromJsonAsync<X509CacheSettings>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X509 store cache");
            return null;
        }
    }
    public async Task<CrlFileCacheSettings?> GetCryptNetUrlCache(string location)
    {
        try
        {
            _logger.LogDebug($"Calling GetCryptNetUrlCache");
           var response = await _httpClient.GetAsync($"Infrastructure/GetCryptNetUrlCache?crlUrl={location}");

            return await response.Content.ReadFromJsonAsync<CrlFileCacheSettings>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file cache");
            return null;
        }
    }

    public async Task RemoveFromX509Store(X509CacheSettings? settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("Infrastructure/RemoveFromX509Store", settings);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from X509 store");
            throw;
        }
    }

    public async Task RemoveFromFileCache(CrlFileCacheSettings? settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("Infrastructure/RemoveFromFileCache", settings);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from file cache");
            throw;
        }
    }
}
