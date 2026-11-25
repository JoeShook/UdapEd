#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;
public  class CertificateViewModel
{
    /// <summary>
    /// Dissect certificate into most used properties
    /// </summary>
    public List<Dictionary<string, string>> TableDisplay { get; set; } = new List<Dictionary<string, string>>();

    public long Size { get; set; }

    /// <summary>
    /// OpenSSL or CertUtil verbose display
    /// </summary>
    public string PlatformToolDisplay { get; set; }

    /// <summary>
    /// Set when the certificates came from a JWT x5c header that does not carry the
    /// certificate that issued the signing certificate.
    /// </summary>
    public string? X5cWarning { get; set; }
}
