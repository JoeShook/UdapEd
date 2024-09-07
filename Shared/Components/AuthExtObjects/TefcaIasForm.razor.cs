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
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback<Dictionary<string, TEFCAIASAuthorizationExtension>> OnInclude { get; set; }
    [Parameter] public EventCallback<Dictionary<string, TEFCAIASAuthorizationExtension>> OnRemove { get; set; }
    [Parameter] public string? Id { get; set; }
    private MudForm form;
    private TEFCAIASAuthorizationExtension hl7B2BModel = new TEFCAIASAuthorizationExtension();
    
    private string selectedConsentPolicy;
    private string selectedConsentReference;
    private string newConsentPolicy;
    private string newConsentReference;
    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    protected override async Task OnInitializedAsync()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(authExtObj.Value.Json) ??
                          new TEFCAIASAuthorizationExtension();
        }
        else
        {
            // default starter template
            hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":\"urn:oid:2.16.840.1.113883.5.8#TREAT\"}");
        }
    }

    public void Update()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            hl7B2BModel = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(authExtObj.Value.Json) ??
                          new TEFCAIASAuthorizationExtension();
        }
    }

    private void AddConsentPolicy()
    {
        if (!string.IsNullOrWhiteSpace(newConsentPolicy) && !hl7B2BModel.ConsentPolicy.Contains(newConsentPolicy))
        {
            hl7B2BModel.ConsentPolicy.Add(newConsentPolicy);
            newConsentPolicy = string.Empty;
        }
    }

    private async Task RemoveConsentPolicy(string policy)
    {
        var remove = hl7B2BModel.ConsentPolicy?.Remove(policy);
        Console.WriteLine("Removed: " + remove);
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, hl7B2BModel, true);
        StateHasChanged();
        await Task.Delay(100);
    }

    private void AddConsentReference()
    {
        if (!string.IsNullOrWhiteSpace(newConsentReference) &&
            !hl7B2BModel.ConsentReference.Contains(newConsentReference))
        {
            hl7B2BModel.ConsentReference.Add(newConsentReference);
            newConsentReference = string.Empty;
        }
    }

    private async Task RemoveConsentReference(string reference)
    {
        hl7B2BModel.ConsentReference?.Remove(reference);
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, hl7B2BModel, true);
        StateHasChanged();
    }

    private async Task HandleInclude()
    {
        await form.Validate();
        if (form.IsValid)
        {
            var b2bAuthExtensions = await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, hl7B2BModel, true);
            await OnInclude.InvokeAsync(b2bAuthExtensions);
        }
    }

    private async Task HandleRemove()
    {
        var b2bAuthExtensions = await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.TEFCAIAS, hl7B2BModel, false);
        await OnRemove.InvokeAsync(b2bAuthExtensions);
    }

    private async Task<Dictionary<string, TEFCAIASAuthorizationExtension>> UpdateAppState(string key, TEFCAIASAuthorizationExtension model, bool use)
    {
        var jsonString = model.SerializeToJson(true);

        if (AppState.AuthorizationExtObjects.ContainsKey(key))
        {
            AppState.AuthorizationExtObjects[key].Json = jsonString;
            AppState.AuthorizationExtObjects[key].Use = use;
        }
        else
        {
            AppState.AuthorizationExtObjects.Add(key, new AuthExtModel
            {
                Json = jsonString,
                Use = use
            });
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationExtObjects), AppState.AuthorizationExtObjects);

        var updatedState = AppState.AuthorizationExtObjects
            .Where(a => a.Value.Use)
            .ToDictionary(
                a => a.Key,
                a => JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(a.Value.Json, _jsonSerializerOptions)
            );

        return updatedState;
    }

    private async Task GoToTEFCAFacilitateFhirSOP()
    {
        await JSRuntime.InvokeVoidAsync("open", "https://rce.sequoiaproject.org/wp-content/uploads/2024/07/SOP-Facilitated-FHIR-Implementation_508-1.pdf#page=17", "_blank");
    }
}
