﻿#region (c) 2024 Joseph Shook. All rights reserved.
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

public partial class Hl7B2BUserForm
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public string? Id { get; set; } [Parameter] public AuthExtObjectOperationType OperationType { get; set; }

    private MudForm _form = null!;
    private HL7B2BUserAuthorizationExtension _hl7B2BModel = new HL7B2BUserAuthorizationExtension();
    private string? _jsonUserPerson = null;
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
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BUserAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BUserAuthorizationExtension();
        }
        else
        {
            // default starter template
            _hl7B2BModel = JsonSerializer.Deserialize<HL7B2BUserAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":[\"urn:oid:2.16.840.1.113883.5.8#TREAT\"]}");
        }

        _jsonUserPerson = _hl7B2BModel?.UserPerson?.GetRawText();
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
        if (!string.IsNullOrWhiteSpace(_newPurposeOfUse) && _hl7B2BModel.PurposeOfUse != null && !_hl7B2BModel.PurposeOfUse.Contains(_newPurposeOfUse))
        {
            _hl7B2BModel.PurposeOfUse.Add(_newPurposeOfUse);
            _newPurposeOfUse = string.Empty;
        }
    }

    private async Task RemovePurposeOfUse(string purpose)
    {
        if (_hl7B2BModel.PurposeOfUse != null)
        {
            purpose = purpose.Trim();

            // Directly manipulate the list
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
        Console.WriteLine("Removed: " + remove);
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
        ParseJsonUserPerson();
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
            AppState.AuthorizationExtObjects[key].UseInAuth = use;
        }
        else
        {
            AppState.AuthorizationExtObjects.Add(key, new AuthExtModel
            {
                Json = jsonString,
                UseInAuth = use
            });
        }

        return AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationExtObjects), AppState.AuthorizationExtObjects);
    }

    private async Task GoToFhirIdentityMatchingIg()
    {
        await JsRuntime.InvokeVoidAsync("open", "https://build.fhir.org/ig/HL7/fhir-identity-matching-ig/patient-matching.html#consumer-match", "_blank");
    }

    private string? ValidateJsonUserPerson(string? input)
    {
        ParseJsonUserPerson();
        var result = _hl7B2BModel.UserPerson == null ? "Invalid Person Resource" : null;
        return result;
    }

    private void ParseJsonUserPerson()
    {
        if (!string.IsNullOrEmpty(_jsonUserPerson))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(_jsonUserPerson);
                _hl7B2BModel.UserPerson = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                _hl7B2BModel.UserPerson = null;
            }
        }
        else
        {
            _hl7B2BModel.UserPerson = null;
        }
    }
}