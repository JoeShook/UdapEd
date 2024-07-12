using System.Net.Http;
using System.Net.Http.Json;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class MutualTlsService : IMutualTlsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MutualTlsService> _logger;

    public MutualTlsService(HttpClient httpClientClient, ILogger<MutualTlsService> logger)
    {
        _httpClient = httpClientClient;
        _logger = logger;
    }

    public async Task UploadClientCertificate(string certBytes)
    {
        var response = await _httpClient.PostAsJsonAsync("MutualTLS/UploadClientCertificate", certBytes);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var response = await _httpClient.PutAsJsonAsync("MutualTLS/UploadTestClientCertificate", certificateName);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        var result = await _httpClient.PostAsJsonAsync(
            "MutualTLS/ValidateCertificate",
            password);

        if (!result.IsSuccessStatusCode)
        {
            Console.WriteLine(await result.Content.ReadAsStringAsync());

            return new CertificateStatusViewModel
            {
                CertLoaded = CertLoadedEnum.Negative
            };
        }

        return await result.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> ClientCertificateLoadStatus()
    {
        var response = await _httpClient.GetFromJsonAsync<CertificateStatusViewModel>("MutualTLS/IsClientCertificateLoaded");

        return response;
    }

    public async Task<CertificateStatusViewModel?> UploadAnchorCertificate(string certBytes)
    {
        var result = await _httpClient.PostAsJsonAsync("MutualTLS/UploadAnchorCertificate", certBytes);
        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> LoadAnchor()
    {
        var response = await _httpClient.PutAsJsonAsync("MutualTLS/LoadAnchor", "http://crl.fhircerts.net/certs/SureFhirmTLS_CA.cer");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> AnchorCertificateLoadStatus()
    {
        var response = await _httpClient.GetFromJsonAsync<CertificateStatusViewModel>("MutualTLS/IsAnchorCertificateLoaded");

        return response;
    }

    public async Task<List<string>?> VerifyMtlsTrust(string publicCertificate)
    {
        try
        {
            var udapMetadataUrl = $"MutualTLS/VerifyMtlsTrust";
            var response = await _httpClient.PostAsJsonAsync(udapMetadataUrl, publicCertificate);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<string>?>();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal Error: Failed POST /MutualTLS");
            return new List<string>(){ "Internal Error: Failed POST /MutualTLS" };
        }
    }
}
