#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Model;

public class OutgoingRequestInfo
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<HeaderItem> Headers { get; set; } = [];
    public string? Body { get; set; }

    public class HeaderItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
