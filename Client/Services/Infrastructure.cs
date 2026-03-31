#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using static MudBlazor.CategoryTypes;

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

    public async Task<byte[]> BuildTefcaTestCertificatePackage(List<string> subjAltNames)
    {
        var queryString = string.Join("&", subjAltNames.Select(name => $"subjAltNames={Uri.EscapeDataString(name)}"));
        var response = await _httpClient.GetAsync($"Infrastructure/BuildTefcaTestCertificatePackage?{queryString}");

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

    public async Task<CertificateViewModel?> GetX509ViewModel(string url)
    {
        try{
            var response = await _httpClient.GetAsync($"Infrastructure/GetX509ViewModel?url={url}");

            return await response.Content.ReadFromJsonAsync<CertificateViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GetCertificateData from list");
            return null;
        }
    }

    public async Task<string?> GetIntermediateX509(string url, string? certContext = null)
    {
        try
        {
            var requestUrl = $"Infrastructure/AddIntermediateX509?url={url}";
            if (!string.IsNullOrEmpty(certContext))
            {
                requestUrl += $"&certContext={Uri.EscapeDataString(certContext)}";
            }
            var response = await _httpClient.GetAsync(requestUrl);

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from X509 store");
            throw;
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

    public async Task EnableFhirCompression(bool enable)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"Infrastructure/SetFhirCompression", enable);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to set FHIR compression setting. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed SetFhirCompression to {enable}");
            throw;
        }
    }

    public async Task<bool> GetFhirCompression()
    {
        try
        {
            var response = await _httpClient.GetAsync($"Infrastructure/GetFhirCompression");

            if (response.IsSuccessStatusCode)
            {
                var enabled = await response.Content.ReadFromJsonAsync<bool>();
                return enabled;
            }

            _logger.LogError($"Failed to get FHIR compression setting. Status code: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed GetFhirCompression");
            throw;
        }
    }

    public async Task EnableCertificateCache(bool enable)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"Infrastructure/SetCertificateCache", enable);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to set certificate cache setting. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed SetCertificateCache to {enable}");
            throw;
        }
    }

    public async Task<bool> GetCertificateCacheEnabled()
    {
        try
        {
            var response = await _httpClient.GetAsync($"Infrastructure/GetCertificateCache");

            if (response.IsSuccessStatusCode)
            {
                var enabled = await response.Content.ReadFromJsonAsync<bool>();
                return enabled;
            }

            _logger.LogError($"Failed to get certificate cache setting. Status code: {response.StatusCode}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed GetCertificateCache");
            throw;
        }
    }

    public async Task ClearAiaCache(string url)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Infrastructure/ClearAiaCache", url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to clear AIA cache. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed ClearAiaCache");
            throw;
        }
    }

    public async Task ClearCrlCache(string url)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Infrastructure/ClearCrlCache", url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to clear CRL cache. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed ClearCrlCache");
            throw;
        }
    }
}