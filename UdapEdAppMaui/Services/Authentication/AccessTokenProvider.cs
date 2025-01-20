#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Microsoft.Extensions.Logging;
using UdapEd.Shared;
using UdapEd.Shared.Services.Authentication;

namespace UdapEdAppMaui.Services.Authentication;

public class AccessTokenProvider : IAccessTokenProvider
{
    private readonly ILogger<AccessTokenProvider> _logger;

    public AccessTokenProvider(ILogger<AccessTokenProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAccessToken(CancellationToken token = default)
    {
        var accessToken = await SecureStorage.Default.GetAsync(UdapEdConstants.TOKEN);
        _logger.LogDebug($"AccessTokenProvider.GetAccessToken: {accessToken}");
        return accessToken;
    }
}
