using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Shared;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Pages;

public partial class PatientSearch
{
    private MudForm form = null!;
    private MudTable<Patient> _table = null!;
    public PatientSearchModel _model  = new();
    public FhirTablePager? _pager = null!;
    // private List<Patient>? _patients;

    private Hl7.Fhir.Model.Bundle? _currentBundle;
    public Hl7.Fhir.Model.Bundle? CurrentBundle => _currentBundle;

    private string? _outComeMessage;
    private string _selectedItemText = string.Empty;

    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;

    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    private string? _baseUrlOverride = string.Empty;

    private string? BaseUrlOverride
    {
        get
        {
            if (string.IsNullOrEmpty(_baseUrlOverride))
            {
                _baseUrlOverride = AppState.BaseUrl;
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
        if (_table != null)
        {
            _table.CurrentPage = 0;
            await _table.ReloadServerData();
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
        if (AppState.BaseUrl != null && _searchEnabled)
        {
            if (_currentBundle != null)
            {
                _currentBundle.Entry.RemoveAll(component => true); // We only need the links for paging.
                _model.Bundle = await new FhirJsonSerializer().SerializeToStringAsync(_currentBundle);
            }
            
            _model.RowsPerPage = AppState.PatientSearchPref.RowsPerPage;

            if (_pager != null) {_model.PageDirection = _pager.PageDirection;}
            
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
        if (args.Row.IsChecked)
        {
            _selectedItemText = new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                .SerializeToString(args.Item);
        }
        else
        {
            _selectedItemText = string.Empty;
        }
    }
}