﻿#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;
public class ClientCredentialsTokenRequestModel
{
    public string? ClientId { get; set; }
    public string? TokenEndpointUrl { get; set; }

    public string? Scope { get; set; }

    public Dictionary<string, object>? Extensions { get; set; }
}