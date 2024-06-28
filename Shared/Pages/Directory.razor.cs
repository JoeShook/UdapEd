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
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
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
    private string _resultRaw = string.Empty;
    private string? _outComeMessage;
    private List<Hl7.Fhir.Model.Bundle.EntryComponent?>? _entries;

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
        _entries = null;
        StateHasChanged();
        await T.Task.Delay(100);

        var result = await FhirService.SearchGet($" {_fhirSearch.BaseUrl}/{_fhirSearch.ResourceName}?{_searchString}");
        await SearchHandler(result);
    }

    private async T.Task SearchPost()
    {
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
            _entries = null;
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
                _resultRaw = $"HTTP/{result.Version} {(int)result.HttpStatusCode} {result.HttpStatusCode}";
                _resultRaw += Environment.NewLine + Environment.NewLine;
            }

            _resultRaw += await new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                .SerializeToStringAsync(result.Result);

            _entries = result.Result?.Entry
                .Where(e => e.Resource is Resource)
                .Select(e => e)
                .ToList();
        }
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
