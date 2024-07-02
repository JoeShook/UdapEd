using static Hl7.Fhir.Model.SearchParameter;

namespace UdapEd.Shared.Search;
public class FhirSearch
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public List<FhirSearchParam> SearchParams { get; set; } = [];

    public string? SortField { get; set; }

    public string SortOrder { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public int ResultCountPerPage { get; set; } = 10;
    
    public Dictionary<string, bool> Includes = new Dictionary<string, bool>();
    public Dictionary<string, bool> RevIncludes = new Dictionary<string, bool>();

    public void ResetParameters()
    {
        SearchParams.Clear();
        Includes.Clear();
        RevIncludes.Clear();
    }
}


public class FhirSearchParam(string name, string modifierName, string? modifier, string value)
{
    public int Id { get; set; }

    public string Name { get; set; } = name;

    public string ModifierName { get; set; } = modifierName;

    public string? Modifier { get; set; } = modifier;

    public string Value { get; set; } = value;
}

