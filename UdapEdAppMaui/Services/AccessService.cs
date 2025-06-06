﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Udap.Client.Client.Extensions;
using Udap.Model.Access;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Mappers;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;
internal class AccessService : IAccessService
{
    readonly HttpClient _httpClient;
    private readonly ILogger<AccessService> _logger;

    public AccessService(HttpClient httpClient, ILogger<AccessService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AccessCodeRequestResult?> Get(string authorizeQuery)
    {
        var handler = new HttpClientHandler() { AllowAutoRedirect = false };
        var httpClient = new HttpClient(handler);
#if ANDROID || IOS || MACCATALYST || WINDOWS
        var response = await httpClient.GetAsync(authorizeQuery, cancellationToken: default);
#else
        var response = await httpClient
            .GetAsync(Base64UrlEncoder.Decode(authorizeQuery), cancellationToken: default);
#endif
        var cookies = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value;

        try
        {
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Found)
            {
                var message = await response.Content.ReadAsStringAsync(default);
                _logger.LogWarning(message);

                return new AccessCodeRequestResult
                {
                    Message = $"{response.StatusCode}:: {message}",
                    IsError = true
                };
            }

            var result = new AccessCodeRequestResult
            {
                RedirectUrl = response.Headers.Location?.AbsoluteUri,
                Cookies = cookies
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return null;
        }
    }

    public async Task<UdapAuthorizationCodeTokenRequestModel?> BuildRequestAccessTokenForAuthCode(AuthorizationCodeTokenRequestModel tokenRequestModel, string signingAlgorithm)
    {
        var clientCertWithKey = await RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            _logger.LogWarning("Cannot find a certificate.  Reload the certificate.");
            return null;
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);

        var tokenRequestBuilder = AccessTokenRequestForAuthorizationCodeBuilder.Create(
            tokenRequestModel.ClientId,
            tokenRequestModel.TokenEndpointUrl,
            clientCert,
            tokenRequestModel.RedirectUrl?.ToPlatformScheme(),
            tokenRequestModel.Code);

        var tokenRequest = tokenRequestBuilder.Build(signingAlgorithm);

        if (tokenRequestModel.CodeVerifier != null)
        {
            tokenRequest.CodeVerifier = tokenRequestModel.CodeVerifier;
        }

        return await Task.FromResult(tokenRequest.ToModel());
    }

    public async Task<UdapClientCredentialsTokenRequestModel?> BuildRequestAccessTokenForClientCredentials(ClientCredentialsTokenRequestModel tokenRequestModel,
        string signingAlgorithm)
    {
        var clientCertWithKey = await RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            _logger.LogWarning("Cannot find a certificate.  Reload the certificate.");
            return null;
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);

        var tokenRequestBuilder = AccessTokenRequestForClientCredentialsBuilder.Create(
            tokenRequestModel.ClientId,
            tokenRequestModel.TokenEndpointUrl,
            clientCert);

        if (tokenRequestModel.Extensions != null)
        {
            foreach (var extension in tokenRequestModel.Extensions)
            {
                tokenRequestBuilder.WithExtension(extension.Key, extension.Value);
            }
        }


        if (tokenRequestModel.Scope != null)
        {
            tokenRequestBuilder.WithScope(tokenRequestModel.Scope);
        }

        var tokenRequest = tokenRequestBuilder.Build(signingAlgorithm);

        return await Task.FromResult(tokenRequest.ToModel());
    }

    public async Task<TokenResponseModel?> RequestAccessTokenForClientCredentials(UdapClientCredentialsTokenRequestModel request)
    {
        var tokenRequest = request.ToUdapClientCredentialsTokenRequest();
        var tokenResponse = await _httpClient.UdapRequestClientCredentialsTokenAsync(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType,
            Headers = tokenResponse.HttpResponse?.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToArray())
                is { } dict
                ? JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
                : null
        };

        if (tokenResponseModel.AccessToken != null)
        {
            await SecureStorage.Default.SetAsync(UdapEdConstants.TOKEN, tokenResponseModel.AccessToken);
        }

        return tokenResponseModel;
    }

    public async Task<TokenResponseModel?> RequestAccessTokenForAuthorizationCode(UdapAuthorizationCodeTokenRequestModel request)
    {
        var tokenRequest = request.ToUdapAuthorizationCodeTokenRequest();
        var tokenResponse = await _httpClient.ExchangeCodeForTokenResponse(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType
        };

        if (tokenResponseModel.AccessToken != null)
        {
            await SecureStorage.Default.SetAsync(UdapEdConstants.TOKEN, tokenResponseModel.AccessToken);
        }

        return tokenResponseModel;
    }

    public Task<bool> DeleteAccessToken()
    {
        SecureStorage.Default.Remove(UdapEdConstants.TOKEN);
        return Task.FromResult(true);
    }

    private async Task<string?> RetrieveFromChunks(string baseKey)
    {
        string? totalChunksStr = await SecureStorage.Default.GetAsync($"{baseKey}_totalChunks");
        if (totalChunksStr == null)
        {
            return null;
        }

        int totalChunks = int.Parse(totalChunksStr);
        var data = new List<byte>();

        for (int i = 0; i < totalChunks; i++)
        {
            string chunkKey = $"{baseKey}_chunk_{i}";
            string? chunkBase64 = await SecureStorage.Default.GetAsync(chunkKey);
            if (chunkBase64 != null)
            {
                byte[] chunk = Convert.FromBase64String(chunkBase64);
                data.AddRange(chunk);
            }
        }

        return Convert.ToBase64String(data.ToArray());
    }

}
