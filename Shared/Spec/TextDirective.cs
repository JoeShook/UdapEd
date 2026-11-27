#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Spec;

/// <summary>
/// What a link highlights in the guide: a text fragment (#:~:text=, what a browser's "Copy link to highlight" makes).
/// <see cref="Suffix"/> is text that must come right after <see cref="Start"/> (only whitespace between); it picks
/// out one occurrence without being highlighted itself. For example, a metadata parameter's name in the first column
/// of a table, followed by its "required" badge, rather than the same name inside another row's description.
/// </summary>
public sealed record TextDirective(string Start, string? Suffix = null)
{
    public static implicit operator TextDirective(string start) => new(start);

    /// <summary>The value of the text= directive, percent-encoded.</summary>
    public string Encode() =>
        SpecCatalog.TextFragment(Start) + (Suffix == null ? string.Empty : ",-" + SpecCatalog.TextFragment(Suffix));

    public override string ToString() => Start;
}
