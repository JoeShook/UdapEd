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
public static class UdapClientCredentialsTokenRequestMapper
{
    /// <summary>
    /// Maps a <see cref="UdapClientCredentialsTokenRequest"/> to a <see cref="UdapClientCredentialsTokenRequestModel"/>.
    /// </summary>
    /// <param name="request">The UdapClientCredentialsTokenRequest.</param>
    /// <returns></returns>
    public static UdapClientCredentialsTokenRequestModel ToModel(this UdapClientCredentialsTokenRequest request)
    {
        return new UdapClientCredentialsTokenRequestModel
        {
            Udap = request.Udap,
            GrantType = request.GrantType,
            Scope = request.Scope,
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
