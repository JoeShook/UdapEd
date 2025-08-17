#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class NullExternalWebAuthenticator : IExternalWebAuthenticator
{
    public Task<ExternalWebAuthenticatorResult> AuthenticateAsync(string url, string callbackUrl)
    {
        throw new NotImplementedException("External web authentication is not supported on this platform.");
    }
}
