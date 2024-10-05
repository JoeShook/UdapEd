#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services.Authentication;

public interface IAccessTokenProvider
{
    Task<string?> GetAccessToken(CancellationToken token = default);
}
