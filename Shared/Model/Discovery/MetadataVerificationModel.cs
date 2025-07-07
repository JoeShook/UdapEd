#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Udap.Model;

namespace UdapEd.Shared.Model.Discovery;

public class MetadataVerificationModel
{
    public UdapMetadata? UdapServerMetaData { get; set; }

    /// <summary>
    /// Certificate problems
    /// </summary>
    public List<string> Problems { get; set; } = new List<string>();

    /// <summary>
    /// Certificate Untrusted
    /// </summary>
    public List<string> Untrusted { get; set; } = new List<string>();


    /// <summary>
    /// Jwt Token validation errors
    /// </summary>
    public List<string> TokenErrors { get; set; } = new List<string>();
}
