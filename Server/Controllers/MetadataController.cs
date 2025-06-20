#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Udap.Client.Client;
using Udap.Client.Configuration;
using Udap.Common.Certificates;
using Udap.Common.Extensions;
using Udap.Common.Models;
using Udap.Model;
using Udap.Smart.Model;
using Udap.Util.Extensions;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Discovery;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class MetadataController : Controller
{
    private readonly IUdapClient _udapClient;
    private readonly ILogger<MetadataController> _logger;
    private readonly HttpClient _httpClient;
    private readonly UdapClientOptions _udapClientOptions;

    public MetadataController(IUdapClient udapClient, HttpClient httpClient, IOptionsMonitor<UdapClientOptions> udapClientOptions, ILogger<MetadataController> logger)
    {
        _udapClient = udapClient;
        _httpClient = httpClient;
        _udapClientOptions = udapClientOptions.CurrentValue;
        _logger = logger;
    }

    // get fully validated metadata from .well-known/udap  
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string metadataUrl, [FromQuery] string community)
    {
        var anchorString = HttpContext.Session.GetString(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);

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

            
            _udapClient.Problem += element =>
                result.Notifications.Add(element.ChainElementStatus.Summarize(TrustChainValidator.DefaultProblemFlags));
            _udapClient.Untrusted += certificate2 => result.Notifications.Add("Untrusted: " + certificate2.Subject);
            _udapClient.TokenError += message => result.Notifications.Add("TokenError: " + message);

            await _udapClient.ValidateResource(
                metadataUrl, 
                trustAnchorStore,
                community);
            
            result.UdapServerMetaData = _udapClient.UdapServerMetaData;
            HttpContext.Session.SetString(UdapEdConstants.BASE_URL, metadataUrl);

            return Ok(result);
        }

        return BadRequest("Missing anchor");
    }

    /// <summary>
    /// This is run from the WASM client instead via DirectoryService.cs
    /// Leaving here for experimentation.
    /// </summary>
    /// <param name="metadataUrl"></param>
    /// <returns></returns>
    [HttpGet("metadata")]
    public async Task<IActionResult> Get([FromQuery] string metadataUrl)
    {
        try
        {
            var client = new FhirClient(metadataUrl);
            var result = await client.CapabilityStatementAsync();

            if (result == null)
            {
                return NotFound();
            }
            
            return Ok(await result.ToJsonAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading metadata from {metadataUrl}", metadataUrl);
            return BadRequest();
        }
    }

    [HttpGet("Smart")]
    public async Task<IActionResult> GetSmartMetadata([FromQuery] string metadataUrl)
    {
        try
        {
            var httpClient = new HttpClient();
            return Ok(await httpClient.GetFromJsonAsync<SmartMetadata>(metadataUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading metadata from {metadataUrl}", metadataUrl);
            throw;
        }
    }

    // get metadata from .well-known/udap  that is not validated and trust is not validated
    [HttpGet("UnValidated")]
    public async Task<IActionResult> GetUnValidated([FromQuery] string metadataUrl, [FromQuery] string community)
    {
        var baseUrl = metadataUrl;

        if (!metadataUrl.Contains(UdapConstants.Discovery.DiscoveryEndpoint))
        {
             baseUrl = baseUrl.EnsureTrailingSlash() + UdapConstants.Discovery.DiscoveryEndpoint;
        }
        
        if (!string.IsNullOrEmpty(community))
        {
            baseUrl += $"?{UdapConstants.Community}={community}";
        }

        var model = new MetadataVerificationModel
        {
            Notifications = new List<string>
            {
                "UDAP anchor certificate is not loaded."
            }
        };

        _logger.LogDebug(baseUrl);

        try
        {
            var response = await _httpClient.GetStringAsync(baseUrl);
            var result = JsonSerializer.Deserialize<UdapMetadata>(response);
            model.UdapServerMetaData = result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

        }

        return Ok(model);
    }

    [HttpPost("UploadAnchorCertificate")]
    public IActionResult UploadAnchorCertificate([FromBody] string base64String)
    {
        var result =  new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative, UserSuppliedCertificate = true};

        try
        {
            var certBytes = Convert.FromBase64String(base64String);
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            HttpContext.Session.SetString(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE, base64String);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading certificate");
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(base64String)} {base64String}");
            
            return BadRequest(result);
        }
    }

    [HttpPut("LoadUdapOrgAnchor")]
    public async Task<IActionResult> LoadUdapOrgAnchor([FromBody] string anchorCertificate)
    {
        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative };

        try
        {
            var response = await _httpClient.GetAsync(new Uri(anchorCertificate));
            response.EnsureSuccessStatusCode();
            var certBytes = await response.Content.ReadAsByteArrayAsync();
            var certificate =  X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            HttpContext.Session.SetString(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE, Convert.ToBase64String(certBytes));

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading anchor from {anchorCertificate}", anchorCertificate);
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(anchorCertificate)} {anchorCertificate}");

            return BadRequest(result);
        }
    }

    [HttpGet("IsAnchorCertificateLoaded")]
    public IActionResult IsAnchorCertificateLoaded()
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            var base64String = HttpContext.Session.GetString(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);

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

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return Ok(result);
        }
    }

    [HttpPut]
    public IActionResult SetBaseFhirUrl([FromBody] string? baseFhirUrl, [FromQuery] bool resetToken)
    {
        if (baseFhirUrl == null)
        {
            return new NoContentResult();
        }

        HttpContext.Session.SetString(UdapEdConstants.BASE_URL, baseFhirUrl);

        if (resetToken)
        {
            HttpContext.Session.Remove(UdapEdConstants.TOKEN);
        }

        return Ok();
    }

    // TODO: If I start building public and confidential base clients this should not exist?
    // Well confidential should not need this but public would.
    [HttpPut("Token")]
    public IActionResult SetToken([FromBody] string token)
    {
        HttpContext.Session.SetString(UdapEdConstants.TOKEN, token);

        return Ok();
    }


    [HttpGet("MyIp")]
    public IActionResult Get()
    {
        return Ok(Environment.GetEnvironmentVariable("MyIp"));
    }

    [HttpPut("SetClientHeaders")]
    public IActionResult SetClientHeaders([FromBody] Dictionary<string, string> headers)
    {
        // HttpContext.Session.SetString(UdapEdConstants.CLIENT_HEADERS, JsonSerializer.Serialize<Dictionary<string, string>>(headers));
        _udapClientOptions.Headers = headers;

        return Ok();
    }

    [HttpGet("FhirLabsCommunityList")]
    public async Task<IActionResult> GetFhirLabsCommunityList()
    {
        var response = await _httpClient.GetStringAsync("https://fhirlabs.net/fhir/r4/.well-known/udap/communities/ashtml");
        
        return Ok(response);
    }

    [HttpPost("CertificateDisplayFromJwtHeader")]
    public IActionResult BuildCertificateDisplay([FromBody] List<string> certificates)
    {
        long base64StringSize = System.Text.Encoding.UTF8.GetByteCount(certificates.First());
        var certBytes = Convert.FromBase64String(certificates.First());
        var cert = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
        var result = new CertificateDisplayBuilder(cert).BuildCertificateDisplayData();

        if (result != null)
        {
            result.Size = base64StringSize;
        }

        return Ok(result);
    }

    [HttpPost("CertificateDisplay")]
    public IActionResult BuildCertificateDisplay([FromBody] string certificate)
    {
        var certBytes = Convert.FromBase64String(certificate);
        var cert = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
        var result = new CertificateDisplayBuilder(cert).BuildCertificateDisplayData();

        return Ok(result);

    }
}
