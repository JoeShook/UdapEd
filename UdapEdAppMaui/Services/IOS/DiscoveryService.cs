﻿#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Udap.Model;
using Udap.Smart.Model;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Discovery;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services.IOS;

public class DiscoveryService : IDiscoveryService
{
    readonly HttpClient _httpClient;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(HttpClient httpClient, ILogger<DiscoveryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Get UDAP metadata. Included is a list of <see cref="MetadataVerificationModel.Notifications"/> indicating any reason for failed trust.
    /// </summary>
    /// <param name="metadataUrl"></param>
    /// <param name="community"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<MetadataVerificationModel?> GetUdapMetadataVerificationModel(string metadataUrl, string? community, CancellationToken token)
    {
        try
        {
            var loadedStatus = await AnchorCertificateLoadStatus();

            if (loadedStatus != null && (loadedStatus.CertLoaded == CertLoadedEnum.Positive))
            {
                var udapMetadataUrl = $"Metadata?metadataUrl={metadataUrl}";

                if (community != null)
                {
                    udapMetadataUrl += $"&{UdapConstants.Community}={community}";
                }

                return await _httpClient.GetFromJsonAsync<MetadataVerificationModel>(udapMetadataUrl, token);
            }
            else
            {
                var udapMetadataUrl = $"Metadata/UnValidated?metadataUrl={metadataUrl}";

                if (community != null)
                {
                    udapMetadataUrl += $"&{UdapConstants.Community}={community}";
                }

                return await _httpClient.GetFromJsonAsync<MetadataVerificationModel>(udapMetadataUrl, token);
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET /Metadata?");
            return null;
        }
    }

    public async Task<CapabilityStatement?> GetCapabilityStatement(string url, CancellationToken token)
    {
        try
        {
            //var response = await _httpClient.GetStringAsync($"Metadata/metadata?metadataUrl={url}");
            var response = await _httpClient.GetStringAsync(url);
            var statement = new FhirJsonParser().Parse<CapabilityStatement>(response);
            
            return statement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET /Metadata/metadata");
            return null;
        }
    }

    public Task<SmartMetadata?> GetSmartMetadata(string metadataUrl, CancellationToken token)
    {
        return _httpClient.GetFromJsonAsync<SmartMetadata>($"Metadata/Smart/?metadataUrl={metadataUrl}", token);
    }

    public async Task<CertificateStatusViewModel?> UploadAnchorCertificate(string certBytes)
    {
        var result = await _httpClient.PostAsJsonAsync("Metadata/UploadAnchorCertificate", certBytes);
        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }
    
    public async Task<CertificateStatusViewModel?> LoadFhirLabsAnchor()
    {
        var response = await _httpClient.PutAsJsonAsync("Metadata/LoadUdapOrgAnchor", "https://storage.googleapis.com/crl.fhircerts.net/certs/SureFhirLabs_CA.cer");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> LoadUdapOrgAnchor()
    {
        var response = await _httpClient.PutAsJsonAsync("Metadata/LoadUdapOrgAnchor", "http://certs.emrdirect.com/certs/EMRDirectTestCA.crt");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation(await response.Content.ReadAsStringAsync());
        }

        return await response.Content.ReadFromJsonAsync<CertificateStatusViewModel>();
    }

    public async Task<CertificateStatusViewModel?> AnchorCertificateLoadStatus()
    {
        var response = await _httpClient.GetFromJsonAsync<CertificateStatusViewModel>("Metadata/IsAnchorCertificateLoaded");

        return response;
    }

    public async Task<bool> SetBaseFhirUrl(string? baseFhirUrl, bool resetToken = false)
    {
        var response = await _httpClient.PutAsJsonAsync($"Metadata?resetToken={resetToken}", baseFhirUrl);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> SetClientHeaders(Dictionary<string, string> headers)
    {
        var response = await _httpClient.PutAsJsonAsync("Metadata/SetClientHeaders", headers);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        return false;
    }

    public async Task<CertificateViewModel?> GetCertificateData(IList<string> base64EncodedCertificate, CancellationToken token)
    {
        try
        {
            var udapMetadataUrl = $"Metadata/CertificateDisplayFromJwtHeader";
            var result = await _httpClient.PostAsJsonAsync(udapMetadataUrl, base64EncodedCertificate, cancellationToken: token);

            result.EnsureSuccessStatusCode();

            var contentString = await result.Content.ReadAsStringAsync(token);

            try
            {
                return await result.Content.ReadFromJsonAsync<CertificateViewModel>(cancellationToken: token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize CertificateViewModel. Response content: {Content}", contentString);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GetCertificateData from list");
            return null;
        }
    }

    public async Task<CertificateViewModel?> GetCertificateData(string? base64EncodedCertificate, CancellationToken token)
    {
        try
        {
            var udapMetadataUrl = $"Metadata/CertificateDisplay";
            var result = await _httpClient.PostAsJsonAsync(udapMetadataUrl, base64EncodedCertificate, cancellationToken: token);

            result.EnsureSuccessStatusCode();

            var contentString = await result.Content.ReadAsStringAsync(token);

            try
            {
                return await result.Content.ReadFromJsonAsync<CertificateViewModel>(cancellationToken: token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize CertificateViewModel. Response content: {Content}", contentString);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GetCertificateData");
            return null;
        }
    }

    public async Task<string> GetFhirLabsCommunityList()
    {
        var communityResponse = await _httpClient.GetAsync("Metadata/FhirLabsCommunityList");

        if (communityResponse.IsSuccessStatusCode)
        {
            return await communityResponse.Content.ReadAsStringAsync();
        }
        else
        {
            return "Failed to load https://fhirlabs.net/fhir/r4/.well-known/udap/communities/ashtml";
        }
    }
}