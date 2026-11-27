#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Udap.CdsHooks.Model;
using UdapEd.Server.Extensions;
using UdapEd.Shared.Services;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class CdsServicesController : Controller
{
    private readonly ICdsService _cdsService;

    public CdsServicesController(ICdsService cdsService)
    {
        _cdsService = cdsService;
    }

    [HttpGet("FetchCdsServices")]
    public async Task<IActionResult> FetchCdsServices([FromQuery] string discoveryUrl)
    {
        try
        {
            return Ok(await _cdsService.FetchCdsServices(discoveryUrl));
        }
        catch (Exception ex) when (IsServiceProblem(ex))
        {
            return Failed(ex);
        }
    }

    [HttpPost("GetCdsService")]
    public async Task<IActionResult> GetCdsService([FromQuery] string serviceUrl, [FromBody] CdsRequest request)
    {
        try
        {
            return Ok(await _cdsService.GetCdsService(serviceUrl, request));
        }
        catch (Exception ex) when (IsServiceProblem(ex))
        {
            return Failed(ex);
        }
    }

    private static bool IsServiceProblem(Exception ex) =>
        ex is HttpRequestException or ArgumentException or TaskCanceledException or System.Text.Json.JsonException;

    // Unreachable services, bad URLs and non-JSON answers go back as text the page can show.
    private IActionResult Failed(Exception ex) =>
        StatusCode(ex is ArgumentException ? StatusCodes.Status400BadRequest : StatusCodes.Status502BadGateway, ex.Message);
}
