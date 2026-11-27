#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Spec;

namespace UdapEd.Shared.Conformance;

public enum CheckOutcome
{
    /// <summary>The data meets the requirement.</summary>
    Pass,

    /// <summary>The data breaks a SHALL / SHALL NOT.</summary>
    Fail,

    /// <summary>The data breaks a SHOULD / SHOULD NOT, or something worth a second look.</summary>
    Warning,

    /// <summary>Not a verdict: context that helps read the data.</summary>
    Info
}

/// <summary>One requirement from a specification, checked against data UdapEd sent or received.</summary>
/// <param name="SpecKey">The <see cref="Spec.SpecCatalog"/> section the requirement comes from.</param>
/// <param name="Requirement">The requirement in a line, e.g. "signed_metadata is signed with RS256".</param>
/// <param name="Outcome">What the data shows.</param>
/// <param name="Detail">What was found, when it helps, e.g. the actual value.</param>
public sealed record ConformanceCheck(string SpecKey, string Requirement, CheckOutcome Outcome, string? Detail = null)
{
    /// <summary>
    /// The guide's own words for this requirement (an <see cref="IgText"/> constant). The check's chip links to the
    /// section with this text highlighted.
    /// </summary>
    public TextDirective? Highlight { get; init; }

    public static ConformanceCheck Of(string specKey, string requirement, bool met, string? detailWhenNotMet = null, CheckOutcome whenNotMet = CheckOutcome.Fail) =>
        new(specKey, requirement, met ? CheckOutcome.Pass : whenNotMet, met ? null : detailWhenNotMet);
}
