#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using Udap.Model.Registration;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class CertificationService : ICertificationService
{
    readonly HttpClient _httpClient;

    public CertificationService(HttpClient httpClientClient)
    {
        _httpClient = httpClientClient;
    }

    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var response = await _httpClient.PutAsJsonAsync("Certifications/LoadTestCertificate", certificateName);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task UploadCertificate(string certBytes)
    {
        var result = await _httpClient.PostAsJsonAsync("Certifications/UploadCertificate", certBytes);
        result.EnsureSuccessStatusCode();
    }

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatement(UdapCertificationAndEndorsementDocument request, string signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync(
            $"Certifications/BuildSoftwareStatement?alg={signingAlgorithm}",
            request);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<RawSoftwareStatementAndHeader>();
    }

    public async Task<string?> BuildRequestBody(RawSoftwareStatementAndHeader? request, string signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync($"Certifications/BuildRequestBody?alg={signingAlgorithm}", request);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadAsStringAsync();
    }

    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        var result = await _httpClient.PostAsJsonAsync(
            "Certifications/ValidateCertificate",
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
        var response = await _httpClient.GetFromJsonAsync<CertificateStatusViewModel>("Certifications/IsClientCertificateLoaded");

        return response;
    }
}