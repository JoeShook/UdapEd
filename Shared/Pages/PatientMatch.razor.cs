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
using Microsoft.JSInterop;
using MudBlazor;
using System.Net;
using UdapEd.Shared.Components;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Smart;
using UdapEd.Shared.Services;
using Address = UdapEd.Shared.Model.Address;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Pages;

public partial class PatientMatch
{
    private MudForm _form = null!;
    private MudTable<Hl7.Fhir.Model.Bundle.EntryComponent> _table = null!;
    private PatientMatchModel _model = new(); //starting point for building $match fields
    private List<Hl7.Fhir.Model.Bundle.EntryComponent?>? _entries;
    private int? _compressedSize;
    private int? _decompressedSize;
    private string? _matchResultRaw;
    private string? _outComeMessage;
    private string _parametersJson = string.Empty;
    private string _selectedItemText = string.Empty;

    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IFhirService FhirService { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    [Inject] public HtmlSanitizer HtmlSanitizer { get; set; }
    private ErrorBoundary? ErrorBoundary { get; set; }
    private const string ValidStyle = "pre udap-indent-1";
    private const string InvalidStyle = "pre udap-indent-1 jwt-invalid";
    private string? _baseUrlOverride = string.Empty;
    private bool _contactSystemIsInEditMode;
    private bool _addressIsInEditMode;
    private bool _v2IdentifierSystemIsInEditMode;
    private Hl7.Fhir.Model.ValueSet? _identityValueSet;
    private Hl7.Fhir.Model.ValueSet? IdentityValueSet => _identityValueSet;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadIdentityValueSet();
            await SetHeaders();
            await SetBaseUrl();
        }
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

    private async Task LoadIdentityValueSet()
    {
        var result = await FhirService.GetValueSet("http://hl7.org/fhir/us/identity-matching/ValueSet/Identity-Identifier-vs");
        
        if (result.OperationOutcome != null)
        {
            _entries = null;
            string? errorMessage = null;

            foreach (var issue in result.OperationOutcome.Issue)
            {
                errorMessage += $"Error:: Details: {issue.Details?.Text}.<br/>"
                                + $"Diagnostics: {HtmlSanitizer.Sanitize(issue.Diagnostics)}.<br/>"
                                + $"IssueType: {issue.Code}.<br/>";
            }

            _outComeMessage = errorMessage;
        }
        else
        {
            _outComeMessage = null;
            EnrichIdentifierValueset(result.Result);
            _identityValueSet = result.Result;
        }
    }

    //
    // Adding because of this: https://build.fhir.org/ig/HL7/fhir-identity-matching-ig/patient-matching.html#attribute-applicability
    // But I don't think I understand the full intent the "Attribute Applicability" section of the IG yet.
    //
    private void EnrichIdentifierValueset(ValueSet? valueSet)
    {
        if (valueSet == null)
        {
            return;
        }

        valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        {
            System = "http://hl7.org/fhir/sid/us-mbi",
            Code = "MBI",
            Display = "Medicare Beneficiary Identifier (United States)"
        });

        valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        {
            System = "http://hl7.org/fhir/us/identity-matching/ns/HL7Identifier",
            Code = "HL7Identifier",
            Display = "Digital Identifier to assist in patient matching."
        });

        valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        {
            System = "http://terminology.hl7.org/CodeSystem/v2-0203",
            Code = "MB",
            Display = "Member Number (Insurance)"
        });

        valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        {
            System = "http://terminology.hl7.org/CodeSystem/v2-0203",
            Code = "SN",
            Display = "Subscriber Number (Insurance)"
        });

        valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        {
            System = "http://hl7.org/fhir/sid/us-ssn",
            Code = "SSN",
            Display = "Social Security Number"
        });


        // Organization.identifier.system
        // and
        // Practitioner.identifier.system
        // valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
        // {
        //     System = "'http://hl7.org/fhir/sid/us-npi",
        //     Code = "NPI",
        //     Display = "National Provider Identifier"
        // });
    }

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

    private RawResourcePanel? rawResourcePanel;
    
    private void OnRowClick(TableRowClickEventArgs<Hl7.Fhir.Model.Bundle.EntryComponent> args)
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
    private void AddressEditComplete(object address)
    {
        var addressView = (Address)address;

        if (_model.AddressList != null && addressView.Id == 0)
        {
            var maxId = _model.AddressList.Max(cs => cs.Id);
            addressView.Id = ++maxId;
        }

        BuildMatch();
        StateHasChanged();
    }

    private void AddressCancelComplete(object address)
    {
        var addressView = (Address)address;

        if (addressView.Id == 0)
        {
            _model.AddressList?.Remove(addressView);
            StateHasChanged();
        }
    }

    private void ContactSystemEditComplete(object contact)
    {
        var contactView = (ContactSystem)contact;
    
        if (_model.ContactSystemList != null && contactView.Id == 0)
        {
            var maxId = _model.ContactSystemList.Max(cs => cs.Id);
            contactView.Id = ++maxId;
        }
    
        BuildMatch();
        StateHasChanged();
    }

    private void ContactSystemCancelComplete(object contact)
    {
        var contactView = (ContactSystem)contact;

        if (contactView.Id == 0)
        {
            _model.ContactSystemList?.Remove(contactView);
            StateHasChanged();
        }
    }

    private void IdentityValueSetEditComplete(object codeSystem)
    {
        var codeSystemView = (IdentityValueSetValue)codeSystem;

        if (_model.IdentityValueSetList != null && codeSystemView.Id == 0)
        {
            var maxId = _model.IdentityValueSetList.Max(cs => cs.Id);
            codeSystemView.Id = ++maxId;
        }

        var s = IdentityValueSet?.Expansion.Contains.FirstOrDefault(s => s.Code == codeSystemView.Code)?.System;
        codeSystemView.System = s;

        BuildMatch();
        StateHasChanged();
    }

    private void IdentityValueSetCancelComplete(object codeSystem)
    {
        var codeSystemView = (IdentityValueSetValue)codeSystem;

        if (codeSystemView.Id == 0)
        {
            _model.IdentityValueSetList?.Remove(codeSystemView);
            StateHasChanged();
        }
    }

    private void BuildMatch()
    {
        var patient = new Patient();

        patient.Meta = new Meta();
        patient.Meta.ProfileElement.Add(new FhirUri(IdiProfile)); 

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

        if (_model.IdentityValueSetList != null && _model.IdentityValueSetList.Any())
        {
            

            foreach (var valueSetValue in _model.IdentityValueSetList)
            {
                if (!valueSetValue.Value.IsNullOrEmpty())
                {
                    var identifier = new Identifier();
                    var parts = valueSetValue.Value.Split('|');
                    var codeableConcept = new CodeableConcept
                    {
                        Coding = new List<Coding>
                        {
                            new Coding(valueSetValue.System, valueSetValue.Code)
                        }
                    };
                    identifier.Type = codeableConcept;

                    if (parts.Length == 2)
                    {
                        identifier.System = parts.First();
                        var last = parts.Last();
                        identifier.Value = string.IsNullOrWhiteSpace(last) ? null : last;
                    }
                    else
                    {
                        identifier.Value = parts.Last();
                    }

                    patient.Identifier.Add(identifier);
                }
            }
        }

        //
        // var item = new Identifier();
        //
        // patient.Identifier.Add(item);


        var parameters = new Parameters();
        parameters.Id = $"UdapEd.{Guid.NewGuid():N}";

        if (_operation == "$idi-match")
        {
            parameters.Add(UdapEdConstants.PatientMatch.InParameterNames.PATIENT, patient);
            parameters.Meta = new Meta();
            parameters.Meta.ProfileElement.Add(new FhirUri("http://hl7.org/fhir/us/identity-matching/StructureDefinition/idi-match-input-parameters"));
        }
        else
        {
            parameters.Add(UdapEdConstants.PatientMatch.InParameterNames.RESOURCE, patient);
        }
            

        _parametersJson = new FhirJsonSerializer(new SerializerSettings { Pretty = true })
            .SerializeToString(parameters);
    }

    private async Task OnSelectedGenderChanged(IEnumerable<string> obj)
    {
        var gender = obj.FirstOrDefault();

        if (gender != null)
        {
            _model.Gender = (AdministrativeGender)Enum.Parse(typeof(AdministrativeGender), gender);
            BuildMatch();
        }
    }

    private async Task Match()
    {
        _selectedItemText = string.Empty;
        _entries = null;
        _matchResultRaw = null;
        StateHasChanged();
        await Task.Delay(100);

        if (AppState.BaseUrl != null)
        {
            var result = await FhirService.MatchPatient(Operations[Operation], _parametersJson);

            if (result.FhirCompressedSize != null)
            {
                _compressedSize = result.FhirCompressedSize;
            }

            if (result.FhirDecompressedSize != null)
            {
                _decompressedSize = result.FhirDecompressedSize;
            }

            if (result.UnAuthorized)
            {
                _outComeMessage = HttpStatusCode.Unauthorized.ToString();
            }
            else if (result.HttpStatusCode == HttpStatusCode.PreconditionFailed)
            {
                await DiscoveryService.SetBaseFhirUrl(AppState.BaseUrl);
                _outComeMessage = "BaseUrl was reset.  Try again";
            }

            else if (result.OperationOutcome != null)
            {
                _entries = null;
                string? errorMessage = null;

                foreach (var issue in result.OperationOutcome.Issue)
                {
                    errorMessage += $"Error:: Details: {issue.Details?.Text}.<br/>"
                                    + $"Diagnostics: {HtmlSanitizer.Sanitize(issue.Diagnostics)}.<br/>"
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
        await JsRuntime.InvokeVoidAsync("UdapEd.setFocus", "AddressId:0");
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
        await JsRuntime.InvokeVoidAsync("UdapEd.setFocus", "ContactSystemId:0");
        StateHasChanged();

    }

    private async Task AddVIdentityValueSetValue()
    {
        if (_model.IdentityValueSetList == null)
        {
            _model.IdentityValueSetList = new List<Model.IdentityValueSetValue>();
        }

        _model.IdentityValueSetList.Add(new Model.IdentityValueSetValue());

        await Task.Delay(1);
        StateHasChanged();
        await JsRuntime.InvokeVoidAsync("UdapEd.setFocus", "IdentityValueSet:0");
        StateHasChanged();
    }

    private void DeleteContactSystem(ContactSystem contactSystem)
    {
        _contactSystemIsInEditMode = false;
        _model.ContactSystemList?.Remove(contactSystem);
        BuildMatch();
    }

    private void DeleteAddress(Address address)
    {
        _addressIsInEditMode = false;
        _model.AddressList?.Remove(address);
        BuildMatch();
    }

    private void DeleteIdentityValueSet(IdentityValueSetValue codeSystem)
    {
        _v2IdentifierSystemIsInEditMode = false;
        _model.IdentityValueSetList?.Remove(codeSystem);
        BuildMatch();
    }

    private string? IdiProfile
    {
        get => _idiProfile;
        set
        {
            _idiProfile = value;
            BuildMatch();
        }
    }

    private Dictionary<string, string?> IdiProfiles = new Dictionary<string,string?>()
    {
        {"Empty", null},
        {"IDI-Patient", "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient"},
        {"IDI-Patient-L0", "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L0"},
        {"IDI-Patient-L1", "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L1"},
        {"IDI-Patient-L2", "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L2"}
    };

    private string? _idiProfile;


    private string Operation
    {
        get => _operation;
        set
        {
            _operation = value;
            BuildMatch();
        }
    }

    private Dictionary<string, string> Operations = new Dictionary<string, string>()
    {
        { "$match", "match" },
        { "$idi-match", "idi-match" }
    };

    private string _operation = "$match";


    /// <summary>
    /// Calculates the weighted input score for patient matching based on the HL7 IG table.
    /// </summary>
    public int CalculatePatientWeightedInput()
    {
        int total = 0;

        try
        {
            var parameters = new FhirJsonParser().Parse<Parameters>(_parametersJson);



            Patient patient;

            if (_operation == "$idi-match")
            {
                var inputPatient = parameters.Parameter.FirstOrDefault(p => p.Name == "patient")?.Resource;
                patient = inputPatient as Patient;
            }
            else
            {
                var inputPatient = parameters.Parameter.FirstOrDefault(p => p.Name == "resource")?.Resource;
                patient = inputPatient as Patient;
            }

            if (patient == null)
            {
                return total;
            }

            // 5: Passport Number (PPN) and issuing country, Driver’s License Number (DL) or other State ID Number and (in either case) Issuing US State or Territory, or Digital Identifier is weighted 5.
            // Others are weighted 4.
            // (max weight of 10 for this category, even if multiple ID Numbers included)
            int id4Count = 0;
            int id5Count = 0;

            if (patient?.Identifier != null)
            {
                foreach (var id in patient.Identifier)
                {
                    var code = id.Type?.Coding?.FirstOrDefault()?.Code;
                    if ((code == "PPN" || code == "DL" || code == "HL7Identifier") &&
                        !string.IsNullOrWhiteSpace(id.Value))
                    {
                        id5Count++;
                    }
                    else
                    {
                        id4Count++;
                    }
                }
            }

            int id4Weight = Math.Min(id4Count * 4, 10);
            int id5Weight = Math.Min(id5Count * 5, 10);
            total += id5Weight + id4Weight;

            // 4: Address, telecom email/phone, other identifier
            int cat4Count = 0;
            if (patient.Address != null && patient.Address.Any(a =>
                    (!string.IsNullOrWhiteSpace(a.Line?.FirstOrDefault()) &&
                     !string.IsNullOrWhiteSpace(a.PostalCode)) ||
                    (!string.IsNullOrWhiteSpace(a.City) && !string.IsNullOrWhiteSpace(a.State))))
                cat4Count++;

            if (patient.Telecom != null && patient.Telecom.Any(c =>
                    c.System == ContactPoint.ContactPointSystem.Email && !string.IsNullOrWhiteSpace(c.Value)))
                cat4Count++;
            if (patient.Telecom != null && patient.Telecom.Any(c =>
                    c.System == ContactPoint.ContactPointSystem.Phone && !string.IsNullOrWhiteSpace(c.Value)))
                cat4Count++;

            int cat4Weight = Math.Min(cat4Count * 4, 5);
            total += cat4Weight;

            // 3: First Name and Last Name
            var name = patient.Name?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(name?.Given?.FirstOrDefault()) && !string.IsNullOrWhiteSpace(name?.Family))
                total += 3;

            // 2: Date of Birth
            if (!string.IsNullOrWhiteSpace(patient.BirthDate))
                total += 2;
        }
        catch (Exception)
        {
            return total; // Return 0 if the parameters cannot be parsed
        }

        return total;
    }

    private async Task GoToFhirIdentityMatchingIg()
    {
        await JsRuntime.InvokeVoidAsync("open", "https://build.fhir.org/ig/HL7/fhir-identity-matching-ig/", "_blank");
    }

    private string GetIdiProfileTooltip(string? selectedProfile)
    {
        return selectedProfile switch
        {
            "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient" => "(Base Level) The goal of this profile is to describe a data-minimized version of Patient used to convey information about the patient for Identity Matching utilizing the $match operation. Only requires that 'some valuable data' be populated within the Patient resource and utilizes no weighting of element values.",
            "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L0" => "(Level 0 weighting) The goal of this profile is to describe a data-minimized version of Patient used to convey information about the patient for Identity Matching utilizing the $match operation, and prescribe a minimum set of data elements which, when consistent with an identity verification event performed at IDIAL1.5 or higher, meet a combined 'weighted level' of at least 9",
            "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L1" => "(Level 1 weighting) The goal of this profile is to describe a data-minimized version of Patient used to convey information about the patient for Identity Matching utilizing the $match operation, and prescribe a minimum set of data elements which, when consistent with an identity verification event performed at IDIAL1.8 or higher, meet a combined 'weighted level' of at least 10",
            "http://hl7.org/fhir/us/identity-matching/StructureDefinition/IDI-Patient-L2" => "(Level 2 weighting) The goal of this profile is to describe a data-minimized version of Patient used to convey information about the patient for Identity Matching utilizing the $match operation, and prescribe a minimum set of data elements which, when consistent with an identity verification event performed at IAL2/IDIAL2 or higher, meet a combined 'weighted level' of at least 10",
            _ => "Select an IDI-Patient Profile to see more information."
        };
    }

    private bool _showProfilePopover;

    private void ToggleProfilePopover()
    {
        _showProfilePopover = !_showProfilePopover;
    }
}