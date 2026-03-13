#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Udap.Model.Access;
using UdapEd.Shared.Model;

namespace UdapEd.Shared.Mappers;
public static class UdapAuthorizationCodeTokenRequestMapper
{
    /// <summary>
    /// Maps a <see cref="UdapAuthorizationCodeTokenRequest"/> to a <see cref="UdapAuthorizationCodeTokenRequestModel"/>.
    /// </summary>
    /// <param name="request">The UdapAuthorizationCodeTokenRequest.</param>
    /// <returns></returns>
    public static UdapAuthorizationCodeTokenRequestModel ToModel(this UdapAuthorizationCodeTokenRequest request)
    {
        return new UdapAuthorizationCodeTokenRequestModel
        {
            Udap = request.Udap,
            GrantType = request.GrantType,
            Code = request.Code,
            RedirectUri = request.RedirectUri,
            CodeVerifier = request.CodeVerifier,
            DPoPProofToken = request.DPoPProofToken,
            Resource = request.Resource,
            Address = request.Address,
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            ClientAssertion = request.ClientAssertion?.ToModel(),
            ClientCredentialStyle = request.ClientCredentialStyle,
            AuthorizationHeaderStyle = request.AuthorizationHeaderStyle,
            Parameters = request.Parameters,
            RequestUri = request.RequestUri,
            Version = request.Version
        };
    }
}
