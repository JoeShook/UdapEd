#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Udap.Client.Client.Extensions;
using Udap.Model;
using Udap.Model.Access;
using Udap.Model.Statement;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Mappers;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEdAppMaui.Extensions;

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

        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        if (intermediatesStored != null)
        {
            var intermediateCerts = Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();
            if (intermediateCerts != null && intermediateCerts.Any())
            {
                x5cCerts.AddRange(intermediateCerts);
            }
        }

        UdapAuthorizationCodeTokenRequest tokenRequest;

        if (x5cCerts.Count > 1)
        {
            var now = DateTime.UtcNow;
            var jwtPayload = new JwtPayLoadExtension(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                new List<Claim>
                {
                    new(JwtClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer),
                    new(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId()),
                    new(JwtClaimTypes.Subject, tokenRequestModel.ClientId ?? "")
                },
                now,
                now.AddMinutes(5));

            var clientAssertion = SignedSoftwareStatementBuilder<JwtPayLoadExtension>
                .Create(x5cCerts, jwtPayload)
                .Build(signingAlgorithm);

            tokenRequest = new UdapAuthorizationCodeTokenRequest
            {
                Address = tokenRequestModel.TokenEndpointUrl,
                RequestUri = new Uri(tokenRequestModel.TokenEndpointUrl!),
                Code = tokenRequestModel.Code!,
                RedirectUri = tokenRequestModel.RedirectUrl?.ToPlatformScheme() ?? "",
                ClientAssertion = new Duende.IdentityModel.Client.ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = clientAssertion
                },
                Udap = UdapConstants.UdapVersionsSupportedValue
            };
        }
        else
        {
            var tokenRequestBuilder = AccessTokenRequestForAuthorizationCodeBuilder.Create(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                clientCert,
                tokenRequestModel.RedirectUrl?.ToPlatformScheme(),
                tokenRequestModel.Code);

            tokenRequest = tokenRequestBuilder.Build(signingAlgorithm);
        }

        if (tokenRequestModel.CodeVerifier != null)
        {
            tokenRequest.CodeVerifier = tokenRequestModel.CodeVerifier;
        }

        var tokenRequestModel2 = tokenRequest.ToModel();

        if (tokenRequestModel.EnableDPoP)
        {
            tokenRequestModel2.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                clientCert,
                signingAlgorithm,
                "POST",
                tokenRequestModel.TokenEndpointUrl!);

            tokenRequestModel2.DPoPJkt = DPoPProofTokenGenerator.ComputeJwkThumbprint(clientCert);

            await SecureStorage.Default.SetAsync(UdapEdConstants.DPOP_ENABLED, "true");
            await SecureStorage.Default.SetAsync(UdapEdConstants.DPOP_SIGNING_ALG, signingAlgorithm);
        }
        else
        {
            SecureStorage.Default.Remove(UdapEdConstants.DPOP_ENABLED);
            SecureStorage.Default.Remove(UdapEdConstants.DPOP_SIGNING_ALG);
        }

        return await Task.FromResult(tokenRequestModel2);
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

        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        if (intermediatesStored != null)
        {
            var intermediateCerts = Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();
            if (intermediateCerts != null && intermediateCerts.Any())
            {
                x5cCerts.AddRange(intermediateCerts);
            }
        }

        UdapClientCredentialsTokenRequest tokenRequest;

        if (x5cCerts.Count > 1)
        {
            var now = DateTime.UtcNow;
            var jwtPayload = new JwtPayLoadExtension(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                new List<Claim>
                {
                    new(JwtClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer),
                    new(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId()),
                    new(JwtClaimTypes.Subject, tokenRequestModel.ClientId ?? "")
                },
                now,
                now.AddMinutes(5));

            if (tokenRequestModel.Extensions != null)
            {
                var payload = jwtPayload as Dictionary<string, object>;
                payload.Add(UdapConstants.JwtClaimTypes.Extensions, tokenRequestModel.Extensions);
            }

            var clientAssertion = SignedSoftwareStatementBuilder<JwtPayLoadExtension>
                .Create(x5cCerts, jwtPayload)
                .Build(signingAlgorithm);

            tokenRequest = new UdapClientCredentialsTokenRequest
            {
                Address = tokenRequestModel.TokenEndpointUrl,
                ClientAssertion = new Duende.IdentityModel.Client.ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = clientAssertion
                },
                Udap = UdapConstants.UdapVersionsSupportedValue,
                Scope = tokenRequestModel.Scope
            };
        }
        else
        {
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

            tokenRequest = tokenRequestBuilder.Build(signingAlgorithm);
        }

        var tokenRequestModel2 = tokenRequest.ToModel();

        if (tokenRequestModel.EnableDPoP)
        {
            tokenRequestModel2.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                clientCert,
                signingAlgorithm,
                "POST",
                tokenRequestModel.TokenEndpointUrl!);

            tokenRequestModel2.DPoPJkt = DPoPProofTokenGenerator.ComputeJwkThumbprint(clientCert);

            await SecureStorage.Default.SetAsync(UdapEdConstants.DPOP_ENABLED, "true");
            await SecureStorage.Default.SetAsync(UdapEdConstants.DPOP_SIGNING_ALG, signingAlgorithm);
        }
        else
        {
            SecureStorage.Default.Remove(UdapEdConstants.DPOP_ENABLED);
            SecureStorage.Default.Remove(UdapEdConstants.DPOP_SIGNING_ALG);
        }

        return await Task.FromResult(tokenRequestModel2);
    }

    public async Task<TokenResponseModel?> RequestAccessTokenForClientCredentials(UdapClientCredentialsTokenRequestModel request)
    {
        var tokenRequest = request.ToUdapClientCredentialsTokenRequest();

        // Regenerate a fresh DPoP proof with current iat/jti if DPoP is enabled
        var dpopEnabled = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_ENABLED);
        if (dpopEnabled == "true" && !string.IsNullOrEmpty(request.DPoPProofToken))
        {
            var clientCertWithKey = await RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
            var signingAlg = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_SIGNING_ALG);

            if (clientCertWithKey != null && signingAlg != null)
            {
                var certBytes = Convert.FromBase64String(clientCertWithKey);
                var flags = X509KeyStorageFlags.DefaultKeySet;
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    flags |= X509KeyStorageFlags.Exportable;
                }
                var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);

                tokenRequest.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                    clientCert,
                    signingAlg,
                    "POST",
                    tokenRequest.Address!);
            }
        }

        var tokenResponse = await _httpClient.UdapRequestClientCredentialsTokenAsync(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            ErrorDescription = tokenResponse.ErrorDescription,
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

        // Regenerate a fresh DPoP proof with current iat/jti if DPoP is enabled
        var dpopEnabled = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_ENABLED);
        if (dpopEnabled == "true" && !string.IsNullOrEmpty(request.DPoPProofToken))
        {
            var clientCertWithKey = await RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
            var signingAlg = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_SIGNING_ALG);

            if (clientCertWithKey != null && signingAlg != null)
            {
                var certBytes = Convert.FromBase64String(clientCertWithKey);
                var flags = X509KeyStorageFlags.DefaultKeySet;
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    flags |= X509KeyStorageFlags.Exportable;
                }
                var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);

                tokenRequest.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                    clientCert,
                    signingAlg,
                    "POST",
                    tokenRequest.Address!);
            }
        }

        var tokenResponse = await _httpClient.ExchangeCodeForTokenResponse(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            ErrorDescription = tokenResponse.ErrorDescription,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType,
            Headers = tokenResponse.HttpResponse?.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToArray())
                is { } authCodeHeaders
                ? JsonSerializer.Serialize(authCodeHeaders, new JsonSerializerOptions { WriteIndented = true })
                : null
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
