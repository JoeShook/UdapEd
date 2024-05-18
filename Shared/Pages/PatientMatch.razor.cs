using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using MudBlazor;
using Org.BouncyCastle.Utilities.Net;
using Udap.Common.Models;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Shared;
using Address = UdapEd.Shared.Model.Address;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Pages;

public partial class PatientMatch
{
    private MudForm _form = null!;
    private MudTable<Hl7.Fhir.Model.Bundle.EntryComponent> _table = null!;
    private PatientMatchModel _model = new(); //starting point for building $match fields
    private List<Hl7.Fhir.Model.Bundle.EntryComponent?>? _entries;
    private string? _matchResultRaw;
    private string? _outComeMessage;
    private string _parametersJson = string.Empty;
    private string _selectedItemText = string.Empty;

    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    private const string ValidStyle = "pre udap-indent-1";
    private const string InvalidStyle = "pre udap-indent-1 jwt-invalid";
    private string? _baseUrlOverride = string.Empty;
    private bool _contactSystemIsInEditMode;
    private bool _addressIsInEditMode;

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
        if (!string.IsNullOrEmpty(_baseUrlOverride))
        {
            await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrl), _baseUrlOverride);
            await DiscoveryService.SetBaseFhirUrl(_baseUrlOverride);
        }
    }

    public string ValidPatientResourceStyle { get; set; } = ValidStyle;

    private void PersistSoftwareStatement()
    {
        try
        {
            new FhirJsonParser().Parse<Parameters>(_parametersJson);
            ValidPatientResourceStyle = ValidStyle;
        }
        catch
        {
            ValidPatientResourceStyle = InvalidStyle;
        }
    }

    private void Cancel()
    {
        _model = new();
    }

    private void OnRowClick(TableRowClickEventArgs<Hl7.Fhir.Model.Bundle.EntryComponent> args)
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

    private async Task BuildMatch()
    {
        var patient = new Patient();

        if (!string.IsNullOrEmpty(_model.Identifier))
        {
            var parts = _model.Identifier.Split("|");

            if (parts.Length == 2)
            {
                patient.Identifier.Add(new Identifier(parts.First(), parts.Last()));
            }
            else
            {
                patient.Identifier.Add(new Identifier() { Value = parts.First() });
            }
        }

        if (!string.IsNullOrEmpty(_model.Family) ||
            !string.IsNullOrEmpty(_model.Given))
        {
            var humanName = new HumanName();

            if (!string.IsNullOrEmpty(_model.Family))
            {
                humanName.Family = _model.Family;
            }

            if (!string.IsNullOrEmpty(_model.Given))
            {
                humanName.Given = new List<string> { _model.Given };
            }

            patient.Name.Add(humanName);
        }

        patient.Gender = _model.Gender;

        if (_model.BirthDate.HasValue)
        {
            patient.BirthDate = _model.BirthDate.Value.ToString("yyyy-MM-dd");
        }

        if (_model.AddressList != null)
        {
            foreach (var modelAddress in _model.AddressList)
            {
                var address = new Hl7.Fhir.Model.Address();

                var lines = new List<string>();
                if (!modelAddress.Line1.IsNullOrEmpty())
                {
                    lines.Add(modelAddress.Line1!);
                    address.Line = lines;
                }

                if (!modelAddress.City.IsNullOrEmpty())
                {
                    address.City = modelAddress.City;
                }

                if (!modelAddress.State.IsNullOrEmpty())
                {
                    address.State = modelAddress.State;
                }

                if (!modelAddress.PostalCode.IsNullOrEmpty())
                {
                    address.PostalCode = modelAddress.PostalCode;
                }

                patient.Address.Add(address);
            }

        }


        if (_model.ContactSystemList != null && _model.ContactSystemList.Any())
        {
            foreach (var contactSystem in _model.ContactSystemList)
            {
                var contactPoint = new ContactPoint
                {
                    System = contactSystem.ContactPointSystem,
                    Use = contactSystem.ContactPointUse,
                    Value = contactSystem.Value
                };

                patient.Telecom.Add(contactPoint);
            }
        }

        var parameters = new Parameters();
        parameters.Add(UdapEdConstants.PatientMatch.InParameterNames.RESOURCE, patient);

        _parametersJson = await new FhirJsonSerializer(new SerializerSettings { Pretty = true })
            .SerializeToStringAsync(parameters);
    }

    private async Task OnSelectedGenderChanged(IEnumerable<string> obj)
    {
        var gender = obj.FirstOrDefault();

        if (gender != null)
        {
            _model.Gender = (AdministrativeGender)Enum.Parse(typeof(AdministrativeGender), gender);
            await BuildMatch();
        }
    }

    private async Task Match()
    {
        _selectedItemText = string.Empty;
        _entries = null;
        StateHasChanged();
        await Task.Delay(100);

        if (AppState.BaseUrl != null)
        {
            var result = await FhirService.MatchPatient(_parametersJson);

            if (result.UnAuthorized)
            {
                _outComeMessage = HttpStatusCode.Unauthorized.ToString();
            }
            else if (result.HttpStatusCode == HttpStatusCode.PreconditionFailed)
            {
                await DiscoveryService.SetBaseFhirUrl(AppState.BaseUrl);
                _outComeMessage = "BaseUrl was reset.  Try again";
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
                    _matchResultRaw = $"HTTP/{result.Version} {(int)result.HttpStatusCode} {result.HttpStatusCode}";
                    _matchResultRaw += Environment.NewLine + Environment.NewLine;
                }

                _matchResultRaw += await new FhirJsonSerializer(new SerializerSettings { Pretty = true })
                    .SerializeToStringAsync(result.Result);

                // var joe = $"{((Bundle.SearchComponent)result.Result?.Entry.SingleOrDefault().Search).Score.Value}/" +
                //               $"{((Code)((Bundle.SearchComponent)result.Result.Entry.SingleOrDefault().Search)
                //                   .Extension.SingleOrDefault(e => e.Url == \"http://hl7.org/fhir/StructureDefinition/match-grade\").Value).Value}";

                _entries = result.Result?.Entry
                    .Where(e => e.Resource is Patient)
                    .Select(e => e)
                    .ToList();
            }
        }
    }

    private string FormatScoreAndGrade(Hl7.Fhir.Model.Bundle.EntryComponent entry)
    {
        var searchResult = entry.Search;
        var score = searchResult.Score ?? 0;
        var code = searchResult.Extension
            .SingleOrDefault(e => e.Url == "http://hl7.org/fhir/StructureDefinition/match-grade")?.Value as Code;
        var grade = code?.Value;

        return $"{score} / {grade}";
    }

    private async Task AddAddress()
    {
        if (_model.AddressList == null)
        {
            _model.AddressList = new List<Model.Address>();
        }

        _model.AddressList.Add(new Model.Address());

        await Task.Delay(1);
        StateHasChanged();

        await Js.InvokeVoidAsync("UdapEd.setFocus", "AddressId:0");

        StateHasChanged();
    }

    private async Task AddContactSystem()
    {
        if (_model.ContactSystemList == null)
        {
            _model.ContactSystemList = new List<Model.ContactSystem>();
        }

        _model.ContactSystemList.Add(new Model.ContactSystem());


        await Task.Delay(1);
        StateHasChanged();

        await Js.InvokeVoidAsync("UdapEd.setFocus", "ContactSystemId:0");

        StateHasChanged();

    }

    private void DeleteContactSystem(ContactSystem contactSystem)
    {
        _contactSystemIsInEditMode = false;
        _model.ContactSystemList?.Remove(contactSystem);
        StateHasChanged();
    }

    private void DeleteAddress(Address address)
    {
        _addressIsInEditMode = false;
        _model.AddressList?.Remove(address);
        StateHasChanged();
    }
}