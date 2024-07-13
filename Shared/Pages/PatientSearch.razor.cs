using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Smart;
using UdapEd.Shared.Services;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Pages;

public partial class PatientSearch
{
    private MudForm form = null!;
    private MudTable<Patient>? PatientTable { get; set; }
    public PatientSearchModel _model  = new();
    public FhirTablePager? _pager = null!;
    // private List<Patient>? _patients;

    private Hl7.Fhir.Model.Bundle? _currentBundle;
    public Hl7.Fhir.Model.Bundle? CurrentBundle => _currentBundle;

    private string? _outComeMessage;
    private string _selectedItemText = string.Empty;

    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    private ErrorBoundary? ErrorBoundary { get; set; }
    private string? _baseUrlOverride = string.Empty;

    protected override Task OnInitializedAsync()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var queryParams = !string.IsNullOrEmpty(uri.Query) ? QueryHelpers.ParseQuery(uri.Query) : null;
        _baseUrlOverride = queryParams?.GetValueOrDefault("BaseUrl") ?? AppState.BaseUrl;

        return base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await SetHeaders();
        await SetBaseUrl();
    }

    protected override void OnParametersSet()
    {
        ErrorBoundary?.Recover();
    }

    private async Task SetHeaders()
    {
        if (AppState.ClientHeaders?.Headers != null)
        {
            await DiscoveryService.SetClientHeaders(
                AppState.ClientHeaders!.Headers!.ToDictionary(h => h.Name, h => h.Value));
        }
    }
    private async Task SetBaseUrl()
    {
        await DiscoveryService.SetBaseFhirUrl(BaseUrlOverride);
    }

    private string? BaseUrlOverride
    {
        get
        {
            if (string.IsNullOrEmpty(_baseUrlOverride))
            {
                _baseUrlOverride = AppState.BaseUrl;
                DiscoveryService.SetBaseFhirUrl(_baseUrlOverride);
            }

            return _baseUrlOverride;
        }

        set => _baseUrlOverride = value;
    }

    private async Task ChangeBaseUrl()
    {
        await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrl), _baseUrlOverride);
        await DiscoveryService.SetBaseFhirUrl(_baseUrlOverride);
    }

    private bool _searchEnabled = false;

    private async Task Search()
    {
        _searchEnabled = true;
        _selectedItemText = string.Empty;
        _currentBundle = null;
        _model.Bundle = null;
        if (PatientTable != null)
        {
            PatientTable.CurrentPage = 0;
            await PatientTable.ReloadServerData();
        }
    }


    private async Task Get()
    {
        _model.GetResource = true;

        await Search();
    }

    private void Cancel()
    {
        _model = new();
    }

    
    private async Task<TableData<Patient>> Reload(TableState state)
    {
        _selectedItemText = "";

        if (AppState.BaseUrl != null && _searchEnabled)
        {
            if (_currentBundle != null)
            {
                _currentBundle.Entry.RemoveAll(component => true); // We only need the links for paging.
                _model.Bundle = await new FhirJsonSerializer().SerializeToStringAsync(_currentBundle);
            }
            
            _model.RowsPerPage = AppState.PatientSearchPref.RowsPerPage;
            _model.LaunchContext = AppState.LaunchContext;
            if (_pager != null) { _model.PageDirection = _pager.PageDirection; }
            
            var result = await FhirService.SearchPatient(_model);

            if (result.UnAuthorized)
            {
                _outComeMessage = HttpStatusCode.Unauthorized.ToString();
            }
            else if (result.HttpStatusCode == HttpStatusCode.PreconditionFailed)
            {
                var setResult = await DiscoveryService.SetBaseFhirUrl(AppState.BaseUrl);
                _outComeMessage = "BaseUrl was reset.  Try again";
            }
            else if (result.OperationOutCome != null)
            {
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
                _currentBundle = result.Result;
                _model.Page = state.Page;

                var patients = _currentBundle?.Entry.Select(entry => entry.Resource).Cast<Patient>().ToList();

                return new TableData<Patient>() { TotalItems = _currentBundle.Total.Value, Items = patients };
            }
        }

        return new TableData<Patient>(){Items = new List<Patient>()};
    }
    
    private void OnRowClick(TableRowClickEventArgs<Patient> args)
    {
        _selectedItemText = new FhirJsonSerializer(new SerializerSettings { Pretty = true })
            .SerializeToString(args.Item);
        
    }

    private async Task SetLaunchContext(Patient patient)
    {
        var launchContext = new LaunchContext
        {
            Patient = patient.Id
        };

        await AppState.SetPropertyAsync(this, nameof(AppState.LaunchContext), launchContext);
    }
}