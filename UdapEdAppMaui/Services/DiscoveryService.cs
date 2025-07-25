﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Udap.Client.Client;
using Udap.Client.Configuration;
using Udap.Common.Certificates;
using Udap.Common.Extensions;
using Udap.Common.Models;
using Udap.Model;
using Udap.Smart.Model;
using Udap.Util.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Discovery;
using UdapEd.Shared.Services;
using Task = System.Threading.Tasks.Task;
using UdapEd.Shared.Extensions;

namespace UdapEdAppMaui.Services;
internal class DiscoveryService : IDiscoveryService
{
    private readonly IOptionsMonitor<UdapClientOptions> _udapClientOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DiscoveryService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<UdapClientOptions> udapClientOptions,
        ILoggerFactory loggerFactory)
    {
        _udapClientOptions = udapClientOptions;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<DiscoveryService>();
        _loggerFactory = loggerFactory;
    }


    public async Task<MetadataVerificationModel?> GetUdapMetadataVerificationModel(string metadataUrl, string? community, CancellationToken token)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var loadedStatus = await AnchorCertificateLoadStatus();

            if (loadedStatus != null && (loadedStatus.CertLoaded == CertLoadedEnum.Positive))
            {
                var anchorString = await SecureStorage.Default.GetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);

                if (anchorString != null)
                {
                    var result = new MetadataVerificationModel();

                    var certBytes = Convert.FromBase64String(anchorString);
                    var anchorCert = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
                    var trustAnchorStore = new TrustAnchorMemoryStore()
                    {
                        AnchorCertificates = new HashSet<Anchor>
                        {
                            new Anchor(anchorCert)
                        }
                    };

                    // Local functions to handle events
                    void OnProblem(X509ChainElement element) =>
                        result.Problems.Add(
                            $"{element.Certificate.SubjectName.Name} :: \n" +
                            $"{element.ChainElementStatus.Summarize(TrustChainValidator.DefaultProblemFlags)}");
                    void OnUntrusted(X509Certificate2 certificate2) =>
                        result.Untrusted.Add("Untrusted: " + certificate2.Subject);
                    void OnTokenError(string message) =>
                        result.TokenErrors.Add("TokenError: " + message);

                    var clientDiscovery = new UdapClientDiscoveryValidator(
                        new TrustChainValidator(_loggerFactory.CreateLogger<TrustChainValidator>()),
                        _loggerFactory.CreateLogger<UdapClientDiscoveryValidator>()
                        );
                    
                    var udapClient = new UdapClient(
                        httpClient,
                        clientDiscovery,
                        _udapClientOptions,
                        new NullLogger<UdapClient>()
                        );

                    // Register event handlers
                    udapClient.Problem += OnProblem;
                    udapClient.Untrusted += OnUntrusted;
                    udapClient.TokenError += OnTokenError;

                    try
                    {
                        await udapClient.ValidateResource(metadataUrl, trustAnchorStore, community, token: token);

                        result.UdapServerMetaData = udapClient.UdapServerMetaData;
                        await SecureStorage.Default.SetAsync(UdapEdConstants.BASE_URL, metadataUrl);

                        return result;
                    }
                    finally
                    {
                        // Unregister event handlers
                        udapClient.Problem -= OnProblem;
                        udapClient.Untrusted -= OnUntrusted;
                        udapClient.TokenError -= OnTokenError;
                    }
                }

                _logger.LogError("Missing anchor");

                return null;
            }
            else
            {
                var baseUrl = metadataUrl.EnsureTrailingSlash() + UdapConstants.Discovery.DiscoveryEndpoint;
                if (!string.IsNullOrEmpty(community))
                {
                    baseUrl += $"?{UdapConstants.Community}={community}";
                }

                _logger.LogDebug(baseUrl);
                var response = await httpClient.GetStringAsync(baseUrl, token);
                var unvalidatedResult = JsonSerializer.Deserialize<UdapMetadata>(response);
                await SecureStorage.Default.SetAsync(UdapEdConstants.BASE_URL, baseUrl.GetBaseUrlFromMetadataUrl());

                var model = new MetadataVerificationModel
                {
                    UdapServerMetaData = unvalidatedResult,
                    Untrusted = new List<string>
                        {
                            "UDAP anchor certificate is not loaded."
                        }
                };

                return model;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get UDAP metadata");

            return null;
        }
    }

    public async Task<CapabilityStatement?> GetCapabilityStatement(string url, CancellationToken token)
    {
        try
        {
            //var response = await _httpClient.GetStringAsync($"Metadata/metadata?metadataUrl={url}");
            var response = await _httpClientFactory.CreateClient().GetStringAsync(url);
            var statement = new FhirJsonParser().Parse<CapabilityStatement>(response);

            return statement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET /Metadata/metadata");
            return null;
        }
    }

    public async Task<SmartMetadata?> GetSmartMetadata(string metadataUrl, CancellationToken token)
    {
        return await _httpClientFactory.CreateClient().GetFromJsonAsync<SmartMetadata>(metadataUrl, token);
    }

    public async Task<CertificateStatusViewModel?> UploadAnchorCertificate(string base64String)
    {
        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative, UserSuppliedCertificate = true };

        try
        {
            var certBytes = Convert.FromBase64String(base64String);
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            await SecureStorage.Default.SetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE, base64String);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading certificate");
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(base64String)} {base64String}");

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> LoadFhirLabsAnchor()
    {
        var anchorCertificate = "https://storage.googleapis.com/crl.fhircerts.net/certs/SureFhirLabs_CA.cer";

        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative };

        try
        {
            var response = await _httpClientFactory.CreateClient().GetAsync(new Uri(anchorCertificate));
            response.EnsureSuccessStatusCode();
            var certBytes = await response.Content.ReadAsByteArrayAsync();
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            await SecureStorage.Default.SetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE, Convert.ToBase64String(certBytes));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading anchor from {anchorCertificate}", anchorCertificate);
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(anchorCertificate)} {anchorCertificate}");

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> LoadUdapOrgAnchor()
    {
        var anchorCertificate = "http://certs.emrdirect.com/certs/EMRDirectTestCA.crt";

        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative };

        try
        {
            var response = await _httpClientFactory.CreateClient().GetAsync(new Uri(anchorCertificate));
            response.EnsureSuccessStatusCode();
            var certBytes = await response.Content.ReadAsByteArrayAsync();
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            await SecureStorage.Default.SetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE, Convert.ToBase64String(certBytes));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading anchor from {anchorCertificate}", anchorCertificate);
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(anchorCertificate)} {anchorCertificate}");

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> AnchorCertificateLoadStatus()
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {

            var base64String = await SecureStorage.Default.GetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);
            
            if (base64String != null)
            {
                var certBytes = Convert.FromBase64String(base64String);
                var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
                result.DistinguishedName = certificate.SubjectName.Name;
                result.Thumbprint = certificate.Thumbprint;
                result.CertLoaded = CertLoadedEnum.Positive;
                result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return result;
        }
    }

    public async Task<bool> SetBaseFhirUrl(string? baseFhirUrl, bool resetToken = false)
    {
        if (resetToken)
        {
            SecureStorage.Default.Remove(UdapEdConstants.TOKEN);
        }

        if (string.IsNullOrEmpty(baseFhirUrl))
        {
            return false;
        }

        await SecureStorage.Default.SetAsync(UdapEdConstants.BASE_URL, baseFhirUrl.GetBaseUrlFromMetadataUrl());

        return true;
    }

    public Task<bool> SetClientHeaders(Dictionary<string, string> headers)
    {
        _udapClientOptions.CurrentValue.Headers = headers;

        return Task.FromResult(true);
    }

    public async Task<CertificateViewModel?> GetCertificateData(IList<string> base64EncodedCertificate, CancellationToken token)
    {
        try
        {
            await Task.Delay(1, token);
            long base64StringSize = System.Text.Encoding.UTF8.GetByteCount(base64EncodedCertificate.First());
            var certBytes = Convert.FromBase64String(base64EncodedCertificate.First());
            var cert = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            var result = new CertificateDisplayBuilder(cert).BuildCertificateDisplayData();

            if (result != null)
            {
                result.Size = base64StringSize;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load first certificate from list");
            return null;
        }
    }

    public async Task<CertificateViewModel?> GetCertificateData(string? base64EncodedCertificate, CancellationToken token)
    {
        try
        {
            await Task.Delay(1, token);
            var certBytes = Convert.FromBase64String(base64EncodedCertificate!);
            var cert = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            var result = new CertificateDisplayBuilder(cert).BuildCertificateDisplayData();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate");
            return null!;
        }
    }

    public async Task<string> GetFhirLabsCommunityList()
    {
        var communityResponse = await _httpClientFactory.CreateClient().GetAsync("https://fhirlabs.net/fhir/r4/.well-known/udap/communities/ashtml");

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
