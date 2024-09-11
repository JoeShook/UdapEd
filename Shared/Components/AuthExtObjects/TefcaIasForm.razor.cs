#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Model;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;
public partial class TefcaIasForm
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public AuthExtObjectOperationType OperationType { get; set; }

    private MudForm _form = null!;
    private TEFCAIASAuthorizationExtension _hl7B2BModel = new TEFCAIASAuthorizationExtension();
    private string? _jsonRelatedPerson;
    private string? _jsonPatient;
    private string? _selectedConsentPolicy;
    private string? _selectedConsentReference;
    private string? _newConsentPolicy;
    private string? _newConsentReference;

    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    protected override void OnInitialized()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(authExtObj.Value.Json) ??
                          new TEFCAIASAuthorizationExtension();
        }
        else
        {
            // default starter template
            _hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":\"urn:oid:2.16.840.1.113883.5.8#TREAT\"}");
        }

        _jsonRelatedPerson = _hl7B2BModel?.UserInformation?.GetRawText();
        _jsonPatient = _hl7B2BModel?.PatientInformation?.GetRawText();
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
        ParseJsonRelatedPerson();
        ParseJsonPatient();
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
        ParseJsonRelatedPerson();
        var result = _hl7B2BModel.UserInformation == null ? "Invalid Person Resource" : null;
        return result;
    }

    private void ParseJsonRelatedPerson()
    {
        if (!string.IsNullOrEmpty(_jsonRelatedPerson))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(_jsonRelatedPerson);
                _hl7B2BModel.UserInformation = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                _hl7B2BModel.UserInformation = null;
            }
        }
        else
        {
            _hl7B2BModel.UserInformation = null;
        }
    }

    private string? ValidateJsonPatient(string? input)
    {
        ParseJsonPatient();
        var result = _hl7B2BModel.PatientInformation == null ? "Invalid Person Resource" : null;
        return result;
    }

    private void ParseJsonPatient()
    {
        if (!string.IsNullOrEmpty(_jsonPatient))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(_jsonPatient);
                _hl7B2BModel.PatientInformation = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                _hl7B2BModel.PatientInformation = null;
            }
        }
        else
        {
            _hl7B2BModel.PatientInformation = null;
        }
    }
}
