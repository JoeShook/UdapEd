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

public partial class Hl7B2BTefcaForm
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public AuthExtObjectOperationType OperationType { get; set; }

    private MudForm _form = null!;
    private HL7B2BAuthorizationExtension _hl7B2BModel = new HL7B2BAuthorizationExtension();
    private string? _selectedPurposeOfUse;
    private string? _newPurposeOfUse;
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
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2B);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BAuthorizationExtension();
        }
        else
        {
            // default starter template
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":[\"urn:oid:2.16.840.1.113883.5.8#TREAT\"]}");
        }
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
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2B);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BAuthorizationExtension();
        }
    }

    private void AddPurposeOfUse()
    {
        if (!string.IsNullOrWhiteSpace(_newPurposeOfUse) && _hl7B2BModel.PurposeOfUse != null && !_hl7B2BModel.PurposeOfUse.Contains(_newPurposeOfUse))
        {
            _hl7B2BModel.PurposeOfUse.Add(_newPurposeOfUse);
            _newPurposeOfUse = string.Empty;
        }
    }

    private void RemovePurposeOfUse(string purpose)
    {
        _hl7B2BModel.PurposeOfUse?.Remove(purpose);
    }

    private void AddConsentPolicy()
    {
        if (!string.IsNullOrWhiteSpace(_newConsentPolicy) && _hl7B2BModel.ConsentPolicy != null && !_hl7B2BModel.ConsentPolicy.Contains(_newConsentPolicy))
        {
            _hl7B2BModel.ConsentPolicy.Add(_newConsentPolicy);
            _newConsentPolicy = string.Empty;
        }
    }

    private void RemoveConsentPolicy(string policy)
    {
        _hl7B2BModel.ConsentPolicy?.Remove(policy);
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

    private void RemoveConsentReference(string reference)
    {
        _hl7B2BModel.ConsentReference?.Remove(reference);
    }

    private async Task HandleInclude()
    {
        await _form.Validate();
        if (_form.IsValid)
        {
            await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2B, _hl7B2BModel, true);
            await OnUpdateEditor.InvokeAsync();
        }
    }

    private async Task HandleRemove()
    {
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2B, _hl7B2BModel, false);
        await OnUpdateEditor.InvokeAsync();
    }

    private Task UpdateAppState(string key, HL7B2BAuthorizationExtension model, bool use)
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
        await JsRuntime.InvokeVoidAsync("open", "https://rce.sequoiaproject.org/wp-content/uploads/2024/07/SOP-Facilitated-FHIR-Implementation_508-1.pdf#page=16", "_blank");
    }
}