#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Duende.IdentityModel;
using Udap.Model;
using Udap.Model.Registration;
using Udap.Util.Extensions;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;


namespace UdapEd.Client.Services;

public class RegisterService : IRegisterService
{
    readonly HttpClient _httpClient;

    public RegisterService(HttpClient httpClientClient)
    {
        _httpClient = httpClientClient;
    }

    public async Task UploadClientCertificate(string certBytes)
    {
        var result = await _httpClient.PostAsJsonAsync("Register/UploadClientCertificate", certBytes);
        result.EnsureSuccessStatusCode();
    }
    

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatementForClientCredentials(
        UdapDynamicClientRegistrationDocument request, 
        string? signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync(
            $"Register/BuildSoftwareStatement/ClientCredentials?alg={signingAlgorithm}", 
            request);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<RawSoftwareStatementAndHeader>();
    }

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatementForAuthorizationCode(
        UdapDynamicClientRegistrationDocument request,
        string signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync(
            $"Register/BuildSoftwareStatement/AuthorizationCode?alg={signingAlgorithm}", 
            request);
        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<RawSoftwareStatementAndHeader>();
    }

    public async Task<UdapRegisterRequest?> BuildRequestBodyForClientCredentials(
        RawSoftwareStatementAndHeader? request,
        string signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync($"Register/BuildRequestBody/ClientCredentials?alg={signingAlgorithm}", request);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<UdapRegisterRequest>();
    }

    public async Task<UdapRegisterRequest?> BuildRequestBodyForAuthorizationCode(
        RawSoftwareStatementAndHeader? request,
        string signingAlgorithm)
    {
        var result = await _httpClient.PostAsJsonAsync($"Register/BuildRequestBody/AuthorizationCode?alg={signingAlgorithm}", request);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<UdapRegisterRequest>();
    }

    public async Task<ResultModel<RegistrationDocument>?> Register(RegistrationRequest registrationRequest)
    {
        var innerResponse = await _httpClient.PostAsJsonAsync(
            "Register/Register",
            registrationRequest,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

        if (!innerResponse.IsSuccessStatusCode)
        {
            var error = await innerResponse.Content.ReadAsStringAsync();
            Console.WriteLine(error);

            return new ResultModel<RegistrationDocument>(error, innerResponse.StatusCode, innerResponse.Version);
        }

        var resultModel = await innerResponse.Content.ReadFromJsonAsync<ResultModel<RegistrationDocument>>();

        if (resultModel != null && resultModel.ErrorMessage != null)
        {
            try
            {
                var dcrResponseError =
                    JsonSerializer.Deserialize<UdapDynamicClientRegistrationErrorResponse>(resultModel.ErrorMessage);

                resultModel.ErrorMessage =
                    JsonSerializer.Serialize(dcrResponseError,
                        new JsonSerializerOptions
                            { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            catch (Exception ex)
            {
                // EverNorth server is returning a 500 when I send invalid scopes for registration and this absorbs that
                Console.WriteLine(ex.Message);
            }
        }

        return resultModel;
    }

    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {

        var result = await _httpClient.PostAsJsonAsync(
            "Register/ValidateCertificate",
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
        var response = await _httpClient.GetFromJsonAsync<CertificateStatusViewModel>("Register/IsClientCertificateLoaded");

        return response;
    }

    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var response = await _httpClient.PutAsJsonAsync("Register/UploadTestClientCertificate", certificateName);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }
    

    /// <summary>
    /// This service currently gets all scopes from Metadata published supported scopes.
    /// In the future we could maintain session data or local data to retain previous
    /// user preferences.
    /// </summary>
    /// <param name="scopes"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public string GetScopes(ICollection<string>? scopes)
    {
        return scopes.ToSpaceSeparatedString();
    }

    public string? GetScopesForClientCredentials(ICollection<string>? scopes, 
        bool smartV1Scopes = true, 
        bool smartV2Scopes = true)
    {
        if (scopes != null)
        {
            return scopes
                .Where(s => s.StartsWith("system"))
                .Where(s => KeepSmartVersion(s, smartV1Scopes, smartV2Scopes))
                .ToList()
                .ToSpaceSeparatedString();
        }

        return null;
    }

    public string GetScopesForAuthorizationCode(ICollection<string>? 
        scopes, 
        bool tieredOauth = false,
        bool oidcScope = true,
        string? scopeLevel = null, 
        bool smartLaunch = false,
        bool smartV1Scopes = true,
        bool smartV2Scopes = true)
    {
        var published = scopes == null ? new List<string>() : 
            scopes
                .Where(s => KeepSmartVersion(s, smartV1Scopes, smartV2Scopes))
                .ToList();

        var enrichScopes = new List<string>();

        if (tieredOauth)
        {
            enrichScopes.Add(UdapConstants.StandardScopes.Udap);
        }

        if (oidcScope)
        {
            enrichScopes.Add(OidcConstants.StandardScopes.OpenId);
        }

        if (smartLaunch && scopeLevel == "patient")
        { 
            enrichScopes.Add($"launch/{scopeLevel}");
        }

        if (smartLaunch && scopeLevel == "user")
        {
            enrichScopes.Add($"launch/{scopeLevel}");
        }

        if (published.Any() && !scopeLevel.IsNullOrEmpty())
        {
            var selectedScopes = published
                .Where(s => s.StartsWith(scopeLevel))
                .Take(10).ToList();

            enrichScopes.AddRange(selectedScopes);
        }
        else if (!scopeLevel.IsNullOrEmpty())
        {
            enrichScopes.Add($"{scopeLevel}/*.read");
        }
        
        return enrichScopes.ToSpaceSeparatedString();
    }

    private static bool KeepSmartVersion(string scope, bool smartV1Scopes, bool smartV2Scopes)
    {
        if (!smartV1Scopes)
        {
            var smartV1Regex = new Regex(@"^(system|user|patient)[\/].*\.(read|write)$");
            var match = smartV1Regex.Match(scope);
            if (match.Success)
            {
                return false;
            }
        }

        if (!smartV2Scopes)
        {
            var smartV2Regex = new Regex(@"^(system|user|patient)[\/].*\.[cruds]+$");
            var match = smartV2Regex.Match(scope);
            if (match.Success)
            {
                return false;
            }
        }

        return true;
    }
}