#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Common.Extensions;
using Udap.Model;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Discovery;
using UdapEd.Shared.Search;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Search;
using Color = MudBlazor.Color;
using T = System.Threading.Tasks;

namespace UdapEd.Shared.Pages;


public partial class Directory
{
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] IMutualTlsService MtlsService { get; set; } = null!;
    [Inject] private ICapabilityLookup CapabilityLookup { get; set; } = null!;
    [Inject] public required IDialogService DialogService { get; set; }

    private ErrorBoundary? _errorBoundary;
    private readonly FhirSearch _fhirSearch = new(){BaseUrl = "https://national-directory.fast.hl7.org/fhir"};
    private string _searchString = string.Empty;
    private string _postSearchString = string.Empty;
    private List<string>? _postSearchParams;
    private const string ValidStyle = "pre udap-indent-1";
    private FhirResults _fhirResults = new FhirResults();
    private string? _outComeMessage;
    private List<Hl7.Fhir.Model.Bundle.EntryComponent?>? _entries;
    private FhirJsonSerializer _fhirJsonSerializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
/// <summary>
/// Hl7.Fhir.Model.ModelInfo.SupportedResources is a source of truth for Resource names
/// </summary>
private readonly List<string> _supportedResources = new List<string>()
    {
        "Practitioner", "PractitionerRole", "Organization", "OrganizationAffiliation",
        "InsurancePlan", "Endpoint", "Location"
    };


    /// <summary>
    /// Method invoked when the component is ready to start, having received its
    /// initial parameters from its parent in the render tree.
    /// Override this method if you will perform an asynchronous operation and
    /// want the component to refresh when that operation is completed.
    /// </summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.</returns>
    protected override async T.Task OnInitializedAsync()
    {
        await CapabilityLookup.Build();
        await base.OnInitializedAsync();
    }

    protected override void OnParametersSet()
    {
        _errorBoundary?.Recover();
    }

    private async T.Task AddSearchParam()
    {
        var searchParam = new FhirSearchParam();

        if (_fhirSearch.ResourceName == "Organization")
        {
            searchParam = new FhirSearchParam("name", "", null, "FhirLabsOrg");
        }
        else if (_fhirSearch.ResourceName == "Endpoint")
        {
            searchParam = new FhirSearchParam("dynamic-registration-trust-profile", "", null, "udap");
        }


        if (_fhirSearch.SearchParams.Any())
        {
            var paramLookups = CapabilityLookup.SearchParamMap
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

        foreach (var include in _fhirSearch.Includes.Where(i => i.Value))
        {
            queryParameters.Add($"_include={include.Key}");
        }

        foreach (var revInclude in _fhirSearch.RevIncludes.Where(i => i.Value))
        {
            queryParameters.Add($"_revinclude={revInclude.Key}");
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
        _fhirResults = new FhirResults();
        StateHasChanged();
        await T.Task.Delay(100);

        var result = await FhirService.SearchGet($" {_fhirSearch.BaseUrl}/{_fhirSearch.ResourceName}?{_searchString}");
        await SearchHandler(result);
    }

    private async T.Task SearchPost()
    {
        _fhirResults = new FhirResults();
        
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

                var scopeNode = result.Result.ToTypedElement().ToScopedNode();
                var references = scopeNode.Select(
                    "Bundle.entry.resource.descendants().select(reference)");

                if (_fhirSearch.ResourceName == "Endpoint")
                {
                    var endpoints =
                        scopeNode.Select(
                            $"Bundle.entry.resource.select($this).where($this is Endpoint)");
                    await BuildTreeviewForEndpoints(endpoints);
                    return;
                }

                foreach (var referenceContext in scopeNode.Select($"Bundle.entry.resource.where($this.is ({_fhirSearch.ResourceName}))"))
                {
                    var domainResource = referenceContext.ToPoco<DomainResource>();
                    var nameMaybe = domainResource.NamedChildren.FirstOrDefault(nc => nc.ElementName == "name");
                    var resource = new FhirJsonParser().Parse(referenceContext) as DomainResource;
                    var orgEntry = new TreeItemData(nameMaybe.Value?.ToString() ?? resource?.Id ?? "Unknown", resource?.Id ?? "Unknown", $"{resource?.TypeName}/{resource?.Id}");

                    _fhirResults.TreeViewStore.Children?.Add(orgEntry);
                }
                
                foreach (var referenceItem in references)
                {
                    //Organization/FhirLabsOrg
                    var parentReferencedId =
                        scopeNode.Select(
                            $"Bundle.entry.where($this.resource is {_fhirSearch.ResourceName} and $this.fullUrl.endsWith('{referenceItem.Value}')).resource.id");
                    
                    foreach (var typedElement in parentReferencedId)
                    {

                        var parentEntry = _fhirResults.TreeViewStore.Children?.FirstOrDefault(tv => tv.Value?.Id == typedElement.Value as string);
                
                        // Get Endpoint addresses
                        var endpoints =
                            scopeNode.Select(
                                $"Bundle.entry.resource.select($this).where($this is Endpoint and $this.descendants().reference.endsWith('{_fhirSearch.ResourceName}/{typedElement.Value}'))");
                        
                        await BuildTreeviewForEndpoints(endpoints, parentEntry);
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

    private async T.Task BuildTreeviewForEndpoints(IEnumerable<ITypedElement> endpoints, TreeItemData<FhirHierarchyEntry>? parentEntry = null)
    {
        var treeStore = parentEntry ??  _fhirResults.TreeViewStore;
        
        foreach (var endpoint in endpoints)
        {
            var endpointResource = new FhirJsonParser().Parse<Endpoint>(endpoint);
            if (treeStore.Children != null && treeStore.Children.Any(ts => ts.Value?.Id == endpointResource.Id))
            {
                return;
            }
            
            var endPointTreeItem = new TreeItemData(endpointResource.Id, endpointResource.Name, $"{endpointResource?.TypeName}/{endpointResource?.Id}");
            
            treeStore.Children ??= new List<TreeItemData<FhirHierarchyEntry>>();
            treeStore.Children?.Add(endPointTreeItem);

            var dynamicEndpointTypes = endpointResource.ToTypedElement().Select(
                $"extension.where($this.url in 'http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-dynamicRegistration').descendants().select($this.descendants().coding.code)");


            foreach (var dynamicEndpointType in dynamicEndpointTypes)
            {
                if (dynamicEndpointType.Value?.ToString() == "udap")
                {
                    var results =
                        await MetadataService.GetUdapMetadataVerificationModel(
                            $"{endpointResource.Address}", null, default);

                    endPointTreeItem.Children ??= new List<TreeItemData<FhirHierarchyEntry>>();
                    endPointTreeItem.Children.Add(new TreeItemData(
                        id: endpointResource.Id, 
                        name: endpointResource.Name,
                        resourceReference: $"{endpointResource?.TypeName}/{endpointResource?.Id}",
                        icon: Icons.Material.TwoTone.DynamicForm,
                        iconColor: results.Notifications.Any() ? Color.Error : Color.Success,
                        link: AppendUdapWellKnown(endpointResource.Address), 
                        type: dynamicEndpointType.Value?.ToString(), 
                        metadata: results,
                        errorNotifications: results.Notifications));
                }

                if (dynamicEndpointType.Value?.ToString() == "smart")
                {
                    var results =
                        await MetadataService.GetSmartMetadata(
                            $"{endpointResource.Address.EnsureTrailingSlash()}.well-known/smart-configuration", default);

                    endPointTreeItem.Children ??= new List<TreeItemData<FhirHierarchyEntry>>();
                    endPointTreeItem.Children.Add(new TreeItemData(
                        endpointResource.Id,
                        endpointResource.Name,
                        $"{endpointResource?.TypeName}/{endpointResource?.Id}",
                        Icons.Material.TwoTone.SmartDisplay,
                        results == null ? Color.Error : Color.Success,
                        endpointResource.Address,
                        dynamicEndpointType.Value?.ToString()));
                }
            }


            var mTlsEndpointTypes = endpointResource.ToTypedElement().Select(
                $"extension.where($this.url = 'http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-secureExchangeArtifacts').select($this.where($this.descendants().coding.code in 'x509-mtls-certificate'))");


            foreach (var mTlsEndpointType in mTlsEndpointTypes)
            {
                var mtlsEncodeCertificate = GetMtlsServerCertificate(mTlsEndpointType);
                var notifications = await MtlsService.VerifyMtlsTrust(mtlsEncodeCertificate);

                endPointTreeItem.Children ??= new List<TreeItemData<FhirHierarchyEntry>>();
                endPointTreeItem.Children.Add(new TreeItemData(
                    endpointResource.Id,
                    endpointResource.Name,
                    $"{endpointResource?.TypeName}/{endpointResource?.Id}",
                    Icons.Material.TwoTone.DynamicForm,
                    notifications != null && notifications.Any() ? Color.Error : Color.Success,
                    endpointResource.Address,
                    mTlsEndpointType.Select("extension.descendants().coding.code").First().Value?.ToString(),
                    null,
                    notifications,
                    mtlsEncodeCertificate
                    ));
            }


            // var nondynamicEndpointTypes = endpointResource.ToTypedElement().Select(
            //     $"extension.where($this.url != 'http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-dynamicRegistration').descendants().select($this.descendants().coding.code)");
            //
            // foreach (var nonDynamicEndpointType in nondynamicEndpointTypes)
            // {
            //     endPointTreeItem.TreeItems.Add(new FhirHierarchyEntries()
            //     {
            //         Id = endpointResource.Id,
            //         Name = endpointResource.Name,
            //         Link = endpointResource.Address,
            //         Type = nonDynamicEndpointType.Value?.ToString(),
            //         // IconColor = Color.Default,
            //         // Icon = Icons.Material.TwoTone.Link
            //     });
            // }
        }
    }

    private string? AppendUdapWellKnown(string endpointResourceAddress)
    {
        if (!endpointResourceAddress.Contains(UdapConstants.Discovery.DiscoveryEndpoint))
        {
            return endpointResourceAddress.EnsureTrailingSlash() + UdapConstants.Discovery.DiscoveryEndpoint;
        }

        return endpointResourceAddress;
    }

    private static string? GetMtlsServerCertificate(ITypedElement mTlsEndpointType)
    {
        try
        {
            return mTlsEndpointType.Select("extension.where(url = 'certificate').value").FirstOrDefault().Value.ToString();
        }
        catch { return "Can't parse"; }
    }

    private void DynamicallyRegister(FhirHierarchyEntry item)
    {
        AppState.SetProperty(this, nameof(AppState.MetadataVerificationModel), item.UdapMetadata);
        AppState.SetProperty(this, nameof(AppState.BaseUrl), item.Link);
        AppState.SetProperty(this, nameof(AppState.Community), string.Empty);
        Js.InvokeVoidAsync("open", ($"/udapRegistration?BaseUrl={item.Link}"), "_blank");
    }

    private async T.Task OpenMtlsAnchorDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<MtlsAnchorDialog>("Options Dialog");
        await dialog.Result;
        await SearchGet();
    }

    private async T.Task OpenUdapAnchorDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<UdapAnchorDialog>("Options Dialog");
        await dialog.Result;
        await SearchGet();
    }

    public class TreeItemData : TreeItemData<FhirHierarchyEntry>
    {
        public TreeItemData(string id, string name, 
            string resourceReference,
            string? icon = null, Color? iconColor = null,
            string? link = null, string? type = null, 
            MetadataVerificationModel? metadata = null, 
            List<string>? errorNotifications = null,
            string? mtlCertificate = null) : base(new FhirHierarchyEntry())
        {
            Value!.Id = id;
            Value.Name = name;
            Value.ResourceReference = resourceReference;
            Value.IconColor = iconColor ?? default;
            Value.Icon = icon;
            Value.Link = link;
            Value.Type = type;
            Value.UdapMetadata = metadata;
            Value.ErrorNotifications = errorNotifications;
            Value.MtlsServerCertificate = mtlCertificate;
        }
    }

    public record FhirHierarchyEntry
    {
        public string Id;
        public string Name;
        public string ResourceReference;
        public string? Type;
        public string? Link;

        public string? Icon { get; set; }
        public Color IconColor { get; set; } = Color.Warning;

        public string? Metadata { get; set; }

        public List<string>? ErrorNotifications { get; set; } = null;
        public MetadataVerificationModel? UdapMetadata { get; set; } = null;
        public string? MtlsServerCertificate { get; set; }
    }


    record FhirResults
    {
        public string HttpStatus { get; set; }
        public string RawFhir { get; set; }
        public List<Hl7.Fhir.Model.Bundle.EntryComponent>? Entries { get; set; }
        public TreeItemData<FhirHierarchyEntry> TreeViewStore = new (){Children = [] };
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
