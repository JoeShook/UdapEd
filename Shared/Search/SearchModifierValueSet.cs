#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;

namespace UdapEd.Shared.Search;
public static class SearchModifierValueSet
{
    static SearchModifierValueSet()
    {
        SearchModifiers.Add(new SearchModifier("", null));

        var mapping = ModelInfo.ModelInspector.EnumMappings.SingleOrDefault(m => m.Canonical == "http://hl7.org/fhir/ValueSet/search-modifier-code");

        if (mapping?.Members != null)
        {
            foreach (var member in mapping.Members)
            {
                SearchModifiers.Add(new SearchModifier(member.Value.Description ?? member.Value.Code, member.Value.Code));
            }
        }
    }

    public static List<SearchModifier> SearchModifiers { get; set; } = [];
}

public class SearchModifier
{
    /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
    public SearchModifier(string name, string? value)
    {
        Name = name;
        Value = value;
        // Description = description; Not in the firely-net-sdk as a codified thing.
    }

    public string Name { get; set; }
    public string? Value { get; set; }

    // public string Description { get; set; }
}
