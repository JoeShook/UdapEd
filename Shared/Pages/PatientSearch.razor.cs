#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Ganss.Xss;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using System.Net;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Smart;
using UdapEd.Shared.Services;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Pages;

public partial class PatientSearch
{
    private MudForm _form = null!;
    private MudTable<Patient>? PatientTable { get; set; }
    public PatientSearchModel _model  = new();
    public FhirTablePager? _pager = null!;
    // private List<Patient>? _patients;

    private Hl7.Fhir.Model.Bundle? _currentBundle;
    public Hl7.Fhir.Model.Bundle? CurrentBundle => _currentBundle;
    private int? _compressedSize;
    private int? _decompressedSize;
    private string? _fhirResultRaw;

    private string? _outComeMessage;
    private Severity _outcomeSeverity = Severity.Info;   // ADD

    private string _selectedItemText = string.Empty;

    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    [Inject] public HtmlSanitizer HtmlSanitizer { get; set; }
    private ErrorBoundary? ErrorBoundary { get; set; }
    private string? _baseUrlOverride = string.Empty;

    private bool _shouldReloadTable = false;

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
        _shouldReloadTable = true;
        await Search(false);
    }

    private async Task Search(bool getRequest)
    {
        _searchEnabled = true;
        _selectedItemText = string.Empty;
        _currentBundle = null;
        _model.Bundle = null;
        _model.GetResource = getRequest;

        if (PatientTable != null)
        {
            PatientTable.CurrentPage = 0;
            await PatientTable.ReloadServerData();
        }
    }


    private async Task Get()
    {
        _shouldReloadTable = true;
        await Search(true);
    }

    private void Cancel()
    {
        _model = new();
    }

    
    private async Task<TableData<Patient>> Reload(TableState state, CancellationToken ct)
    {
        if (!_shouldReloadTable)
        {
            var patientsCached = _currentBundle?.Entry.Select(e => e.Resource).OfType<Patient>().ToList() ?? new List<Patient>();
            return new TableData<Patient> { TotalItems = _currentBundle?.Total ?? 0, Items = patientsCached };
        }
        _shouldReloadTable = false;

        if (AppState.BaseUrl != null && _searchEnabled)
        {
            if (_currentBundle != null)
            {
                _currentBundle.Entry.RemoveAll(_ => true); // keep paging links only
                _model.Bundle = await new FhirJsonSerializer().SerializeToStringAsync(_currentBundle);
            }

            _model.RowsPerPage = AppState.PatientSearchPref.RowsPerPage;
            _model.LaunchContext = AppState.LaunchContext;
            if (_pager != null) { _model.PageDirection = _pager.PageDirection; }

            var result = await FhirService.SearchPatient(_model, ct);

            if (result.FhirCompressedSize != null) _compressedSize = result.FhirCompressedSize;
            if (result.FhirDecompressedSize != null) _decompressedSize = result.FhirDecompressedSize;

            if (result.UnAuthorized)
            {
                _outComeMessage = HttpStatusCode.Unauthorized.ToString();
                return new TableData<Patient> { Items = [] };
            }

            if (result.HttpStatusCode == HttpStatusCode.PreconditionFailed)
            {
                await DiscoveryService.SetBaseFhirUrl(AppState.BaseUrl);
                _outComeMessage = "BaseUrl was reset.  Try again";
                return new TableData<Patient> { Items = [] };
            }

            // Always capture bundle if present
            _currentBundle = result.Result;
            _fhirResultRaw = null;

            if (result.HttpStatusCode != null)
            {
                _fhirResultRaw = $"HTTP/{result.Version} {(int)result.HttpStatusCode} {result.HttpStatusCode}{Environment.NewLine}{Environment.NewLine}";
            }

            if (_currentBundle != null)
            {
                _fhirResultRaw += await new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                    .SerializeToStringAsync(_currentBundle);
            }

            if (result.OperationOutcome != null)
            {
                var msg = "";
                foreach (var issue in result.OperationOutcome.Issue)
                {
                    msg += $"Outcome Issue:<br/>"
                           + $"Severity: {issue.Severity}<br/>"
                           + $"Details: {issue.Details?.Text}<br/>"
                           + $"Diagnostics: {HtmlSanitizer.Sanitize(issue.Diagnostics)}<br/>"
                           + $"Type: {issue.Code}<br/><br/>";
                }

                _outComeMessage = msg;

                // Determine severity (data + non-critical outcome => downgrade)
                var hasNonOutcomeData = _currentBundle?.Entry.Any(e => e.Resource is not OperationOutcome) ?? false;
                _outcomeSeverity = DetermineOutcomeSeverity(result.OperationOutcome, hasNonOutcomeData);
            }
            else
            {
                _outComeMessage = null;
                _outcomeSeverity = Severity.Info;
            }

            _model.Page = state.Page;
            var patients = _currentBundle?.Entry.Select(e => e.Resource).OfType<Patient>().ToList() ?? new List<Patient>();
            return new TableData<Patient> { TotalItems = _currentBundle?.Total ?? 0, Items = patients };
        }

        return new TableData<Patient> { Items = [] };
    }

    // ADD: helper severity mapper
    private static Severity DetermineOutcomeSeverity(OperationOutcome outcome, bool hasData)
    {
        // Rank severities (higher number => more severe)
        static int Rank(OperationOutcome.IssueSeverity? sev) => sev switch
        {
            OperationOutcome.IssueSeverity.Fatal => 4,
            OperationOutcome.IssueSeverity.Error => 3,
            OperationOutcome.IssueSeverity.Warning => 2,
            OperationOutcome.IssueSeverity.Information => 1,
            _ => 1
        };

        var maxRank = outcome.Issue.Any()
            ? outcome.Issue.Max(i => Rank(i.Severity))
            : 1;

        // If we have normal resources plus only info/warning -> downgrade to info/warning
        if (hasData && maxRank <= 2)
        {
            return maxRank == 2 ? Severity.Warning : Severity.Info;
        }

        // Map final
        return maxRank switch
        {
            >= 4 => Severity.Error, // fatal
            3 => Severity.Error,    // error
            2 => Severity.Warning,
            _ => Severity.Info
        };
    }
    
    private RawResourcePanel? rawResourcePanel;
    
    private void OnRowClick(TableRowClickEventArgs<Patient> args)
    {
        rawResourcePanel?.ShowResource(
            new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                .SerializeToString(args.Item)
        );
    }

    private async Task SetLaunchContext(Patient patient)
    {
        var launchContext = new LaunchContext
        {
            Patient = patient.Id
        };
        
        await AppState.SetPropertyAsync(this, nameof(AppState.LaunchContext), launchContext);
    }

    private async Task SetPatientContext(Patient patient)
    {
        AppState.FhirContext.CurrentPatient = patient;
        await AppState.SetPropertyAsync(this, nameof(AppState.FhirContext), AppState.FhirContext);
    }

    private async Task SetRelatedPersonContext(Patient patient)
    {
        var relatedPerson = new RelatedPerson
        {
            Id = patient.Id,
            Meta = patient.Meta,
            Identifier = patient.Identifier,
            Active = patient.Active,
            Name = patient.Name,
            Telecom = patient.Telecom,
            Gender = patient.Gender,
            BirthDate = patient.BirthDate,
            Address = patient.Address,
            Photo = patient.Photo,
            Communication = new List<RelatedPerson.CommunicationComponent>()
        };

        foreach (var patientCommunication in patient.Communication)
        {
            var relatedPersonCommunication = new RelatedPerson.CommunicationComponent()
            {
                Language = patientCommunication.Language,
                Preferred = patientCommunication.Preferred
            };

            relatedPerson.Communication.Add(relatedPersonCommunication);
        }

        AppState.FhirContext.CurrentRelatedPerson = relatedPerson;
        await AppState.SetPropertyAsync(this, nameof(AppState.FhirContext), AppState.FhirContext);
    }

    private async Task SetPersonContext(Patient patient)
    {
        var person = new Person
        {
            Id = patient.Id,
            Meta = patient.Meta,
            Identifier = patient.Identifier,
            Active = patient.Active,
            Name = patient.Name,
            Telecom = patient.Telecom,
            Gender = patient.Gender,
            BirthDate = patient.BirthDate,
            Address = patient.Address
            // Link = new List<Person.LinkComponent>()
        };

        if (patient.Photo.Any())
        {
            person.Photo = patient.Photo.FirstOrDefault();
        }

        // Map any additional fields as needed
        // foreach (var patientLink in patient.Link)
        // {
        //     var personLink = new Person.LinkComponent
        //     {
        //         
        //         Other = patientLink.Other,
        //         Type = patientLink.Type
        //     };
        //
        //     person.Link.Add(personLink);
        // }

        AppState.FhirContext.CurrentPerson = person;
        await AppState.SetPropertyAsync(this, nameof(AppState.FhirContext), AppState.FhirContext);
    }
}