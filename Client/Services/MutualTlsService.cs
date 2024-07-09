using System.Net.Http;
using System.Net.Http.Json;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class MutualTlsService : IMutualTlsService
{
    private readonly HttpClient _httpClient;

    public MutualTlsService(HttpClient httpClientClient)
    {
        _httpClient = httpClientClient;
    }

    public async Task UploadClientCertificate(string certBytes)
    {
        var result = await _httpClient.PostAsJsonAsync("MutualTLS/UploadClientCertificate", certBytes);
        result.EnsureSuccessStatusCode();
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
}
