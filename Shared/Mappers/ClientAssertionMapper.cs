#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Duende.IdentityModel.Client;
using UdapEd.Shared.Model;

namespace UdapEd.Shared.Mappers;
public static class ClientAssertionMapper
{
    /// <summary>
    /// Maps a <see cref="ClientAssertion"/> to a <see cref="ClientAssertionModel"/>.
    /// </summary>
    /// <param name="request">The ClientAssertion.</param>
    /// <returns></returns>
    public static ClientAssertionModel ToModel(this ClientAssertion request)
    {
        return new ClientAssertionModel
        {
            Type = request.Type,
            Value = request.Value
        };
    }
}
