#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Packages.Hl7Terminology_6_0_2;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Model;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model.AuthExtObjects;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Shared.Components.AuthExtObjects;
public partial class TefcaIasForm : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public AuthExtObjectOperationType OperationType { get; set; }

    private MudForm _form = null!;
    private TEFCAIASAuthorizationExtension _hl7B2BModel = new TEFCAIASAuthorizationExtension();
    private MarkupString? _jsonRelatedPerson;
    private MarkupString? _jsonPatient;
    private string? _selectedConsentPolicy;
    private string? _selectedConsentReference;
    private string? _newConsentPolicy;
    private string? _newConsentReference;
    private List<string> _vsPurposeOfUse = ["T-IAS"];
    private MudMenu relatedPersonMenuRef;
    private MudMenu patientMenuRef;

    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    protected override void OnInitialized()
    {
        var authExtObj =
            AppState.AuthorizationExtObjects.SingleOrDefault(a =>
                a.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(authExtObj.Value.Json) ??
                           new TEFCAIASAuthorizationExtension();
        }
        else
        {
            _hl7B2BModel = new TEFCAIASAuthorizationExtension();
        }

        if (_hl7B2BModel?.UserInformation != null)
        {
            try
            {
                var person = new FhirJsonParser().Parse<RelatedPerson>(_hl7B2BModel?.UserInformation.ToString());
                SetRelatedPersonPresentation(person);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        if (_hl7B2BModel?.PatientInformation != null)
        {
            try
            {
                var patient = new FhirJsonParser().Parse<Patient>(_hl7B2BModel?.PatientInformation.ToString());
                SetPatientPresentation(patient);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private Task<IEnumerable<string>> SearchPurposeOfUse(string value, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(value))
            return Task.FromResult(_vsPurposeOfUse.AsEnumerable());

        return Task.FromResult(_vsPurposeOfUse
            .Where(c => c.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .AsEnumerable());
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            return _form.Validate();
        }

        return Task.CompletedTask;
    }
    
    public void Update()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(authExtObj.Value.Json) ??
                          new TEFCAIASAuthorizationExtension();
        }
    }

    private void AddConsentPolicy()
    {
        if (!string.IsNullOrWhiteSpace(_newConsentPolicy) && _hl7B2BModel.ConsentPolicy != null && !_hl7B2BModel.ConsentPolicy.Contains(_newConsentPolicy))
        {
            _hl7B2BModel.ConsentPolicy.Add(_newConsentPolicy);
            _newConsentPolicy = string.Empty;
        }
    }

    private async Task RemoveConsentPolicy(string policy)
    {
        var remove = _hl7B2BModel.ConsentPolicy?.Remove(policy);
        Console.WriteLine("Removed: " + remove);
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, _hl7B2BModel, true);
        StateHasChanged();
        await Task.Delay(100);
    }

    private void AddConsentReference()
    {
        if (!string.IsNullOrWhiteSpace(_newConsentReference) &&
            _hl7B2BModel.ConsentReference != null &&
            !_hl7B2BModel.ConsentReference.Contains(_newConsentReference))
        {
            _hl7B2BModel.ConsentReference.Add(_newConsentReference);
            _newConsentReference = string.Empty;
        }
    }

    private async Task RemoveConsentReference(string reference)
    {
        _hl7B2BModel.ConsentReference?.Remove(reference);
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, _hl7B2BModel, true);
        StateHasChanged();
    }

    private async Task HandleInclude()
    {
        await _form.Validate();
        if (_form.IsValid)
        {
            await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, _hl7B2BModel, true);
            await OnUpdateEditor.InvokeAsync();
        }
    }

    private async Task HandleRemove()
    {
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, _hl7B2BModel, false);
        await OnUpdateEditor.InvokeAsync();
    }

    private Task UpdateAppState(string key, TEFCAIASAuthorizationExtension model, bool use)
    {
        var jsonString = JsonSerializer.Serialize(model, _jsonSerializerOptions);

        if (AppState.AuthorizationExtObjects.ContainsKey(key))
        {
            AppState.AuthorizationExtObjects[key].Json = jsonString;
            if (OperationType == AuthExtObjectOperationType.Auth)
            {
                AppState.AuthorizationExtObjects[key].UseInAuth = use;
            }
            else if (OperationType == AuthExtObjectOperationType.Register)
            {
                AppState.AuthorizationExtObjects[key].UseInRegister = use;
            }
        }
        else
        {
            var newAuthExtModel = new AuthExtModel
            {
                Json = jsonString
            };

            if (OperationType == AuthExtObjectOperationType.Auth)
            {
                newAuthExtModel.UseInAuth = use;
            }
            else if (OperationType == AuthExtObjectOperationType.Register)
            {
                newAuthExtModel.UseInRegister = use;
            }

            AppState.AuthorizationExtObjects.Add(key, newAuthExtModel);
        }

        return AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationExtObjects), AppState.AuthorizationExtObjects);
    }

    private async Task GoToTefcaFacilitateFhirSOP()
    {
        await JsRuntime.InvokeVoidAsync("open", "https://rce.sequoiaproject.org/wp-content/uploads/2024/07/SOP-Facilitated-FHIR-Implementation_508-1.pdf#page=17", "_blank");
    }

    private string? ValidateJsonRelatedPerson(string? input)
    {
        var result = _hl7B2BModel.UserInformation == null ? "Invalid Person Resource" : null;
        return result;
    }
    

    private void SetRelatedPersonPresentation(RelatedPerson person)
    {
        _jsonRelatedPerson = new MarkupString(string.Join("<br/> ",
            person.Name.Select(hn => $"{hn.Given.First()}, {hn.Family}")));
    }

    private void UseRelatedPersonContext()
    {
        if (AppState.FhirContext.CurrentRelatedPerson != null)
        {
            var jsonString = new FhirJsonSerializer().SerializeToString(AppState.FhirContext.CurrentRelatedPerson);
            _hl7B2BModel.UserInformation = JsonDocument.Parse(jsonString).RootElement;
            SetRelatedPersonPresentation(AppState.FhirContext.CurrentRelatedPerson);
        }
    }

    private async Task EmptyRelatedPersonContext()
    {
        var relatedPerson = new RelatedPerson()
        {
            Name =
            [
                new HumanName()
                {
                    Family = "Newman",
                    Given = new List<string>() { "Alice", "Jones" }
                }
            ]
        };

        var jsonString = await new FhirJsonSerializer().SerializeToStringAsync(relatedPerson);
        _hl7B2BModel.UserInformation = JsonDocument.Parse(jsonString).RootElement;
        SetRelatedPersonPresentation(relatedPerson);
    }

    private void SearchForRelatedPerson()
    {
        NavigationManager.NavigateTo("/patientSearch");
    }

    private void ClearRelatedPersonContext()
    {
        AppState.FhirContext.CurrentRelatedPerson = null;
        _jsonRelatedPerson = null;
    }



    private void SetPatientPresentation(Patient patient)
    {
        _jsonPatient = new MarkupString(string.Join("<br/> ",
            patient.Name.Select(hn => $"{hn.Given.First()}, {hn.Family}")));
    }

    private void UsePatientContext()
    {
        if (AppState.FhirContext.CurrentPatient != null)
        {
            var jsonString = new FhirJsonSerializer().SerializeToString(AppState.FhirContext.CurrentPatient);
            _hl7B2BModel.PatientInformation = JsonDocument.Parse(jsonString).RootElement;
            SetPatientPresentation(AppState.FhirContext.CurrentPatient);
        }
    }

    private async Task EmptyPatientContext()
    {
        var person = new Patient()
        {
            Name =
            [
                new HumanName()
                {
                    Family = "Newman",
                    Given = new List<string>() { "Alice", "Jones" }
                }
            ]
        };

        var jsonString = await new FhirJsonSerializer().SerializeToStringAsync(person);
        _hl7B2BModel.PatientInformation = JsonDocument.Parse(jsonString).RootElement;
        SetPatientPresentation(person);
    }

    private void SearchForPatient()
    {
        NavigationManager.NavigateTo("/patientSearch");
    }

    private void ClearPatientContext()
    {
        AppState.FhirContext.CurrentPatient = null;
        _jsonRelatedPerson = null;
    }

    private async Task OpenRelatedPersonMenu(EventArgs e)
    {
        await relatedPersonMenuRef.ToggleMenuAsync(e);
    }

    private async Task OpenPatientMenu(EventArgs e)
    {
        await patientMenuRef.ToggleMenuAsync(e);
    }
}
