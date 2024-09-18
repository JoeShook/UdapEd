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

namespace UdapEd.Shared.Components;

public partial class Hl7B2BUserForm : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public AuthExtObjectOperationType OperationType { get; set; }

    private MudForm _form = null!;
    private HL7B2BUserAuthorizationExtension _hl7B2BModel = new HL7B2BUserAuthorizationExtension();
    private MarkupString? _jsonUserPerson = null;
    private string? _selectedPurposeOfUse;
    private string? _newPurposeOfUse;
    private string? _selectedConsentPolicy;
    private string? _selectedConsentReference;
    private string? _newConsentPolicy;
    private string? _newConsentReference;
    private List<string> _vsPurposeOfUse;
    private MudMenu personMenuRef;

    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    protected override void OnInitialized()
    {
        _vsPurposeOfUse = Hl7Helpers.GetAllCodingsFromType(typeof(VsPurposeOfUse)).Where(c => c.Code != "PurposeOfUse").Select(c => c.Code).ToList();

        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER);
        
        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BUserAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BUserAuthorizationExtension();
        }
        else
        {
            _hl7B2BModel = new HL7B2BUserAuthorizationExtension();
        }

        if (_hl7B2BModel.UserPerson != null)
        {
            try
            {
                var person = new FhirJsonParser().Parse<Person>(_hl7B2BModel.UserPerson.ToString());
                SetPersonPresentation(person);
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
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BUserAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BUserAuthorizationExtension();
        }
    }

    private void AddPurposeOfUse()
    {
        if (!string.IsNullOrWhiteSpace(_newPurposeOfUse) && _hl7B2BModel.PurposeOfUse != null && !_hl7B2BModel.PurposeOfUse.Contains($"urn:oid:2.16.840.1.113883.5.8#{_newPurposeOfUse}"))
        {
            _hl7B2BModel.PurposeOfUse.Add($"urn:oid:2.16.840.1.113883.5.8#{_newPurposeOfUse}");
            _newPurposeOfUse = string.Empty;
        }
    }

    private async Task RemovePurposeOfUse(string purpose)
    {
        if (_hl7B2BModel.PurposeOfUse != null)
        {
            purpose = purpose.Trim();

            _hl7B2BModel.PurposeOfUse = _hl7B2BModel.PurposeOfUse
                .Where(p => !p.Equals(purpose, StringComparison.OrdinalIgnoreCase))
                .ToList();

            await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER, _hl7B2BModel, true);
            StateHasChanged();
        }
        else
        {
            Console.WriteLine("PurposeOfUse list is null.");
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
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER, _hl7B2BModel, true);
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
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER, _hl7B2BModel, true);
        StateHasChanged();
    }

    private async Task HandleInclude()
    {
        await _form.Validate();
        if (_form.IsValid)
        {
            await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER, _hl7B2BModel, true);
            await OnUpdateEditor.InvokeAsync();
        }
    }

    private async Task HandleRemove()
    {
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER, _hl7B2BModel, false);
        await OnUpdateEditor.InvokeAsync();
    }

    private Task UpdateAppState(string key, HL7B2BUserAuthorizationExtension model, bool use)
    {
        var jsonString = model.SerializeToJson(true);

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

    private async Task GoToFhirIdentityMatchingIg()
    {
        await JsRuntime.InvokeVoidAsync("open", "https://build.fhir.org/ig/HL7/fhir-identity-matching-ig/patient-matching.html#consumer-match", "_blank");
    }

    private string? ValidateJsonUserPerson(string? input)
    {
        var result = _hl7B2BModel.UserPerson == null ? "Invalid Person Resource" : null;
        return result;
    }

    
    private void SetPersonPresentation(Person person)
    {
        _jsonUserPerson = new MarkupString(string.Join("<br/> ",
            person.Name.Select(hn => $"{hn.Given.First()}, {hn.Family}")));
    }

    private void UsePersonContext()
    {
        if (AppState.FhirContext.CurrentPerson != null)
        {
            var jsonString = new FhirJsonSerializer().SerializeToString(AppState.FhirContext.CurrentPerson);
            _hl7B2BModel.UserPerson = JsonDocument.Parse(jsonString).RootElement;
            SetPersonPresentation(AppState.FhirContext.CurrentPerson);
        }
    }

    private async Task EmptyPersonContext()
    {
        var person = new Person()
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
        _hl7B2BModel.UserPerson = JsonDocument.Parse(jsonString).RootElement;
        SetPersonPresentation(person);

    }

    private void SearchForPerson()
    {
        NavigationManager.NavigateTo("/patientSearch");
    }

    private void ClearPersonContext()
    {
        AppState.FhirContext.CurrentPerson = null;
        _jsonUserPerson = null;
    }


    private async Task OpenMenu(EventArgs e)
    {
        await personMenuRef.ToggleMenuAsync(e);
    }
}