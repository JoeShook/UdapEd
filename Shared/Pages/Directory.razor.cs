#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System;
using System.Net;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Navigation;
using Hl7.FhirPath;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Search;
using UdapEd.Shared.Services;
using UdapEd.Shared.Shared;
using T = System.Threading.Tasks;

namespace UdapEd.Shared.Pages;


public partial class Directory
{
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;

    private ErrorBoundary? _errorBoundary;
    private readonly FhirSearch _fhirSearch = new(){BaseUrl = "https://national-directory.fast.hl7.org/fhir"};
    private bool _searchParmIsInEditMode;
    private string _searchString = string.Empty;
    private string _postSearchString = string.Empty;
    private List<string>? _postSearchParams;
    private const string ValidStyle = "pre udap-indent-1";
    private FhirResults _fhirResults = new FhirResults();
    private string? _outComeMessage;
    private List<Hl7.Fhir.Model.Bundle.EntryComponent?>? _entries;
    private Dictionary<string, bool> _includeCheckBoxes = new Dictionary<string, bool>();
    private Dictionary<string, bool> _revIncludeCheckBoxes = new Dictionary<string, bool>();
    private FhirJsonSerializer _fhirJsonSerializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
/// <summary>
/// Hl7.Fhir.Model.ModelInfo.SupportedResources is a source of truth for Resource names
/// </summary>
private readonly List<string> _supportedResources = new List<string>()
    {
        "Practitioner", "PractitionerRole", "Organization", "OrganizationAffiliation",
        "InsurancePlan", "Endpoint", "Location"
    };

    

    protected override void OnParametersSet()
    {
        _errorBoundary?.Recover();
    }

    private async T.Task AddSearchParam()
    {
        var searchParam = new FhirSearchParam("name", "Starts with", null, "FhirLabsOrg");

        if (_fhirSearch.SearchParams.Any())
        {
            var paramLookups = SearchParamLookup.Map
                .Where(m => m.Resource == _fhirSearch.ResourceName)
                .SelectMany(s => s.ParamDefinitions)
                .ToList();

            if (!paramLookups.Any())
            {
                return;
            }

            foreach (var param in paramLookups)
            {
                if (_fhirSearch.SearchParams.Any(pl => pl.Name == param.Code))
                {
                    continue;
                }

                searchParam.Name = param.Code!;
                searchParam.ModifierName = SearchModifierValueSet.SearchModifiers.First().Name;
                searchParam.Modifier = SearchModifierValueSet.SearchModifiers.First().Value;
                if (searchParam.Name == "active")
                {
                    searchParam.Value = "true";
                }
                else if(searchParam.Value != "FhirLabsOrg")
                {
                    searchParam.Value = string.Empty;
                }
                
                break;
            }
        }

        searchParam.Id = _fhirSearch.SearchParams.Any() ? _fhirSearch.SearchParams.Max(sp => sp.Id) + 1 : 0;
        _fhirSearch.SearchParams.Add(searchParam);

        await T.Task.Delay(1);
        BuildSearch();
    }

    private void DeleteParam(FhirSearchParam fhirSearchParam)
    {
        _searchParmIsInEditMode = false;
        _fhirSearch.SearchParams?.Remove(fhirSearchParam);
        BuildSearch();
        StateHasChanged();
    }
    
    private void ActiveModifierChanged(bool obj)
    {
        BuildSearch();
    }

    private void BuildSearch()
    {
        _searchString = string.Empty;
        _postSearchString = string.Empty;
        var queryParameters = new List<string>();

        foreach (var param in _fhirSearch.SearchParams)
        {
            var modifier = SearchModifierValueSet.SearchModifiers.Find(sm => sm.Name == param.ModifierName)?.Value;
            queryParameters.Add($"{param.Name}{modifier.Prefix(":")}={param.Value}");
           
        }

        foreach (var cb in _includeCheckBoxes.Where(i => i.Value))
        {
            queryParameters.Add($"_include={cb.Key}");
        }

        foreach (var cb in _revIncludeCheckBoxes.Where(i => i.Value))
        {
            queryParameters.Add($"_revinclude={cb.Key}");
        }

        _searchString = string.Join('&', queryParameters);
        _postSearchString = string.Join("\n", queryParameters);
        _postSearchParams = queryParameters;

    }

    private void BuildManualGetSearch(string obj)
    {
        _searchString = obj;
    }

    private void BuildManualPostSearch(string obj)
    {
        _postSearchParams = obj.Split("\n").ToList();
    }

    private async T.Task SearchGet()
    {
        _fhirResults.Entries = null;
        StateHasChanged();
        await T.Task.Delay(100);

        var result = await FhirService.SearchGet($" {_fhirSearch.BaseUrl}/{_fhirSearch.ResourceName}?{_searchString}");
        await SearchHandler(result);
    }

    private async T.Task SearchPost()
    {
        _fhirResults.Entries = null;
        StateHasChanged();
        await T.Task.Delay(100);

        var formSearch = new SearchForm()
        {
            Url = _fhirSearch.BaseUrl,
            Resource = _fhirSearch.ResourceName,
            FormUrlEncoded = _postSearchParams
        };

        var result = await FhirService.SearchPost(formSearch);
        await SearchHandler(result);
    }

    private async T.Task SearchHandler(FhirResultModel<Hl7.Fhir.Model.Bundle> result)
    {
        if (result.UnAuthorized)
        {
            _outComeMessage = HttpStatusCode.Unauthorized.ToString();
        }
        else if (result.HttpStatusCode == HttpStatusCode.PreconditionFailed)
        {
            // await DiscoveryService.SetBaseFhirUrl(AppState.BaseUrl);
            _outComeMessage = "PreconditionFailed";
        }

        else if (result.OperationOutCome != null)
        {
            _fhirResults.Entries = null;
            string? errorMessage = null;

            foreach (var issue in result.OperationOutCome.Issue)
            {
                errorMessage += $"Error:: Details: {issue.Details?.Text}.<br/>"
                                + $"Diagnostics: {issue.Diagnostics}.<br/>"
                                + $"IssueType: {issue.Code}.<br/>";
            }

            _outComeMessage = errorMessage;
        }
        else
        {
            _outComeMessage = null;

            if (result.HttpStatusCode != null)
            {
                _fhirResults.HttpStatus = $"HTTP/{result.Version} {(int)result.HttpStatusCode} {result.HttpStatusCode}";
            }

            if (result.Result != null)
            {
                _fhirResults.RawFhir = await new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                    .SerializeToStringAsync(result.Result);

                var r = result.Result.GetResources(); //.Where(r => r.GetType() == typeof(Endpoint));

                Hl7.FhirPath.FhirPathCompiler.DefaultSymbolTable.AddFhirExtensions();

                foreach (var resource in result.Result.GetResources())
                {
                    Console.WriteLine(
                        $"{resource.ToTypedElement().InstanceType}/{resource.ToTypedElement().ToScopedNode().Id()}");

                }

                var scopeNode = result.Result.ToTypedElement().ToScopedNode();
                var OrgsRefedByEndoints = scopeNode.Select(
                    "Bundle.entry.resource.where($this is Endpoint).descendants().select(reference)");

                   
                foreach (var orgsRefedByEndoint in OrgsRefedByEndoints)
                {
                    Console.WriteLine(orgsRefedByEndoint.Value);

                    //Organization/FhirLabsOrg

                    Console.WriteLine(
                        $"Bundle.entry.where($this.resource is Organization and $this.fullUrl.endsWith('{orgsRefedByEndoint.Value}')).resource.id");
                    // Get Name of Org
                    var OrgNames =
                        scopeNode.Select(
                            $"Bundle.entry.where($this.resource is Organization and $this.fullUrl.endsWith('{orgsRefedByEndoint.Value}')).resource.id");

                    foreach (var typedElement in OrgNames)
                    {
                        var orgEntry = new FhirHierarchyEntries() { Name = typedElement.Value?.ToString() };
                        _fhirTreeViewStore.Add(orgEntry);

                        Console.WriteLine(typedElement.Value);

                        Console.WriteLine(
                            $"Bundle.entry.resource.select($this).where($this is Endpoint and $this.descendants().reference.endsWith('Organization/{typedElement.Value}'))");
                        // Get Endpoint addresses
                        var endpoints =
                            scopeNode.Select(
                                $"Bundle.entry.resource.select($this).where($this is Endpoint and $this.descendants().reference.endsWith('Organization/{typedElement.Value}'))");
                        
                        foreach (var endpoint in endpoints)
                        {
                            var epResource = new FhirJsonParser().Parse<Endpoint>(endpoint);
                            orgEntry.TreeItems.Add(new FhirHierarchyEntries() { Name = epResource.Name, Link = epResource.Address });

                            Console.WriteLine(endpoint.Name);
                            Console.WriteLine(epResource.Address);
                        }
                    }
                }

                _fhirResults.Entries = result.Result?.Entry
                    .Where(e => e.Resource != null)
                    .Select(e => e)
                    .ToList();
                
            }
            else
            {
                _fhirResults.RawFhir = string.Empty;

            }
        }
    }

    private HashSet<FhirHierarchyEntries> _fhirTreeViewStore = new HashSet<FhirHierarchyEntries>();
    private FhirHierarchyEntries _fhirHierarchyEntries;

    record FhirHierarchyEntries
    {
        public HashSet<FhirHierarchyEntries> TreeItems = new HashSet<FhirHierarchyEntries>();

        public string Name;

        public string? Link;

        public string Icon { get; set; } = Icons.Custom.FileFormats.FileDocument;
    }


    record FhirResults
    {
        public string HttpStatus { get; set; }
        public string RawFhir { get; set; }
        public List<Hl7.Fhir.Model.Bundle.EntryComponent>? Entries { get; set; }
    }


    public class ObjectToFhirBoolConverter : BoolConverter<string>
    {

        public ObjectToFhirBoolConverter()
        {
            SetFunc = OnSet;
            GetFunc = OnGet;
        }

        private string OnGet(bool? value)
        {
            try
            {
                return value == true ? "true" : "false";
            }
            catch (Exception e)
            {
                UpdateGetError("Conversion error: " + e.Message);
                return default;
            }
        }

        private bool? OnSet(string arg)
        {
            if (arg == null)
                return null;
            try
            {
                if (arg == "true")
                    return true;
                if (arg == "false")
                    return false;
                else
                    return null;
            }
            catch (FormatException e)
            {
                UpdateSetError("Conversion error: " + e.Message);
                return null;
            }
        }

    }
}
