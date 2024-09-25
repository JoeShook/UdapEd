#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UdapEd.Server.Extensions;
using UdapEd.Shared.Services;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class InfrastructureController : Controller
{
    private readonly IInfrastructure _infrastructure;

    public InfrastructureController(IInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
    }

    [HttpGet("BuildMyTestCertificatePackage")]
    public async Task<IActionResult> BuildMyTestCertificatePackage(List<string> subjAltNames, CancellationToken token)
    {
        var zip = await _infrastructure.BuildMyTestCertificatePackage(subjAltNames);
        var base64String = Convert.ToBase64String(zip);

        return Ok(base64String);
    }

    [HttpGet("JitFhirlabsCommunityCertificate")]
    public async Task<IActionResult> JitFhirlabsCommunityCertificate(List<string> subjAltNames, string password, CancellationToken token)
    {
        var clientCertBytes = await _infrastructure.JitFhirlabsCommunityCertificate(subjAltNames, password);
        var base64String = Convert.ToBase64String(clientCertBytes);

        return Ok(base64String);
    }
}
