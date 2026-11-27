#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace UdapEd.Shared.Spec;

public static partial class SpecText
{
    /// <summary>HTML-encodes spec text and marks its RFC 2119 keywords (SHALL, SHOULD NOT, MAY, …) so they stand out.</summary>
    public static MarkupString Highlight(string text) =>
        new(Keywords().Replace(WebUtility.HtmlEncode(text), m => $"<span class=\"udaped-rfc2119\">{m.Value}</span>"));

    [GeneratedRegex(@"\b(SHALL NOT|SHOULD NOT|MUST NOT|SHALL|SHOULD|MUST|NOT REQUIRED|REQUIRED|MAY|OPTIONAL)\b")]
    private static partial Regex Keywords();
}
