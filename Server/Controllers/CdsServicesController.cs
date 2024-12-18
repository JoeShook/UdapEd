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
    public async Task<IActionResult> FetchCdsServices()
    {
        var services = await _cdsService.FetchCdsServices();
        return Ok(services);
    }

    [HttpPost("GetCdsService")]
    public async Task<IActionResult> GetCdsService([FromBody] CdsRequest request)
    {
        var services = await _cdsService.GetCdsService(request);
        return Ok(services);
    }
}