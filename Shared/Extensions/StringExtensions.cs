#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace UdapEd.Shared.Extensions;
public static class StringExtensions
{
    public static string TrimForDisplay(this string input, int length, string? suffix)
    {
        if (input.Length > length)
        {
            input = input.Substring(0, length);
            if (suffix != null)
            {
                input += suffix;
            }
        }

        return input;
    }

    public static string ToPlatformScheme(this string uriString)
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        var uri = new Uri(uriString);

        if (uri.Scheme == "http" || uri.Scheme == "https")
        {
            return $"mauiapp{Uri.SchemeDelimiter}{uri.Authority}{uri.AbsolutePath}";
        }
#endif
        return uriString;
    }

    public static ICollection<string> ToPlatformSchemes(this IEnumerable<string> uriStrings)
    {
        var mauiAppSchemes = new List<string>();

        foreach (var uriString in uriStrings)
        {
            var uri = new Uri(uriString);

            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                var redirectUri = $"mauiapp{Uri.SchemeDelimiter}{uri.Authority}{uri.AbsolutePath}";
                mauiAppSchemes.Add(redirectUri);
            }
            else
            {
                mauiAppSchemes.Add(uriString);
            }
        }

        return mauiAppSchemes;
    }

    public static string HighlightScope(this string input)
    {
        var regExp = new Regex("\"scope\":\\s*\".*\"", RegexOptions.Multiline);
        var match = regExp.Match(input); //first occurence

        if (match.Success)
        {
            var sb = new StringBuilder(input);
            sb.Insert(match.Index, "<mark>");
            sb.Insert(match.Index + match.Length + 6, "</mark>");

            return sb.ToString();
        }

        return input;
    }

    public static string Prefix(this string? input, string prefix)
    {
        if (input == null)
        {
            return null;
        }

        return prefix + input;
    }

    public static bool IsNullOrEmpty(this string? input)
    {
        return string.IsNullOrEmpty(input);
    }

    public static bool IsEmpty(this StringValues input)
    {
        return input.Count == 0 || input.ToString() == string.Empty;
    }
}
