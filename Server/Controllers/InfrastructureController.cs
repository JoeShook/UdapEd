﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Services;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class InfrastructureController : Controller
{
    private readonly IInfrastructure _infrastructure;
    private readonly ILogger<InfrastructureController> _logger;

    public InfrastructureController(IInfrastructure infrastructure, ILogger<InfrastructureController> logger)
    {
        _infrastructure = infrastructure;
        _logger = logger;
    }

    [HttpGet("BuildMyTestCertificatePackage")]
    public async Task<IActionResult> BuildMyTestCertificatePackage(List<string> subjAltNames, CancellationToken token)
    {
        try
        {
            var zip = await _infrastructure.BuildMyTestCertificatePackage(subjAltNames);
            var base64String = Convert.ToBase64String(zip);

            return Ok(base64String);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build test certificate package");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("JitFhirlabsCommunityCertificate")]
    public async Task<IActionResult> JitFhirlabsCommunityCertificate(List<string> subjAltNames, string password, CancellationToken token)
    {
        try
        {
            var clientCertBytes = await _infrastructure.JitFhirlabsCommunityCertificate(subjAltNames, password);
            var base64String = Convert.ToBase64String(clientCertBytes);

            return Ok(base64String);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get JitFhirlabs community certificate");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("GetX509ViewModel")]
    public async Task<IActionResult> GetX509ViewModel(string url, CancellationToken token)
    {
        try
        {
            var viewModel = await _infrastructure.GetX509ViewModel(url);
            return Ok(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X509 data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("AddIntermediateX509")]
    public async Task<IActionResult> AddIntermediateX509(string url, CancellationToken token)
    {
        try
        {
            var encodedCertificate = await _infrastructure.GetIntermediateX509(url);
            
            if (encodedCertificate != null)
            {
                byte[] certificateBytes = Convert.FromBase64String(encodedCertificate);
                var certificate = new X509Certificate2(certificateBytes);
                var intermediatesStored = HttpContext.Session.GetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
                var intermediateCerts = intermediatesStored.DeserializeCertificates();
                if (intermediateCerts.All(i => i.Thumbprint != certificate.Thumbprint))
                {
                    intermediateCerts.Add(certificate);
                    HttpContext.Session.SetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES, intermediateCerts.SerializeCertificates());
                }
            }

            return Ok(encodedCertificate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X509 data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("GetCrldata")]
    public async Task<IActionResult> GetCrldata(string url, CancellationToken token)
    {
        try
        {
            var viewModel = await _infrastructure.GetCrldata(url);
            return Ok(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get CRL data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("GetX509StoreCache")]
    public async Task<ActionResult<X509CacheSettings?>> GetX509StoreCache([FromQuery] string thumbprint)
    {
        try
        {
            var result = await _infrastructure.GetX509StoreCache(thumbprint);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X509 store cache");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("RemoveFromX509Store")]
    public async Task<IActionResult> RemoveFromX509Store([FromBody] X509CacheSettings? settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings cannot be null");
        }

        try
        {
            await _infrastructure.RemoveFromX509Store(settings);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from X509 store");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("GetCryptNetUrlCache")]
    public async Task<ActionResult<X509CacheSettings?>> GetCryptNetUrlCache([FromQuery] string crlUrl)
    {
        try
        {
            var result = await _infrastructure.GetCryptNetUrlCache(crlUrl);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X509 store cache");
            return StatusCode(500, "Internal server error");
        }
    }


    [HttpPost("RemoveFromFileCache")]
    public async Task<IActionResult> RemoveFromFileCache([FromBody] CrlFileCacheSettings? settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings cannot be null");
        }

        try
        {
            await _infrastructure.RemoveFromFileCache(settings);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from X509 store");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("SetFhirCompression")]
    public async Task<IActionResult> SetFhirCompression([FromBody] bool enable)
    {
        await _infrastructure.EnableFhirCompression(enable);

        return Ok();
    }

    [HttpGet("GetFhirCompression")]
    public async Task<IActionResult> GetFhirCompression()
    {
        var enabled = await _infrastructure.GetFhirCompression();

        return Ok(enabled);
    }
}
