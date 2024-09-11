using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Model;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;

public partial class Hl7B2bForm
{
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback OnUpdateEditor { get; set; }
    [Parameter] public string? Id { get; set; }

    private MudForm form;
    private HL7B2BAuthorizationExtension hl7B2BModel = new HL7B2BAuthorizationExtension();
    private string selectedPurposeOfUse;
    private string newPurposeOfUse;
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
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2B);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BAuthorizationExtension();
        }
        else
        {
            // default starter template
            hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":[\"urn:oid:2.16.840.1.113883.5.8#TREAT\"]}");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await form.Validate();
        }
    }

    public void Update()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2B);

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            hl7B2BModel = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(authExtObj.Value.Json) ??
                          new HL7B2BAuthorizationExtension();
        }
    }

    private void AddPurposeOfUse()
    {
        if (!string.IsNullOrWhiteSpace(newPurposeOfUse) && !hl7B2BModel.PurposeOfUse.Contains(newPurposeOfUse))
        {
            hl7B2BModel.PurposeOfUse.Add(newPurposeOfUse);
            newPurposeOfUse = string.Empty;
        }
    }

    private void RemovePurposeOfUse(string purpose)
    {
        hl7B2BModel.PurposeOfUse.Remove(purpose);
    }

    private void AddConsentPolicy()
    {
        if (!string.IsNullOrWhiteSpace(newConsentPolicy) && !hl7B2BModel.ConsentPolicy.Contains(newConsentPolicy))
        {
            hl7B2BModel.ConsentPolicy.Add(newConsentPolicy);
            newConsentPolicy = string.Empty;
        }
    }

    private void RemoveConsentPolicy(string policy)
    {
        hl7B2BModel.ConsentPolicy?.Remove(policy);
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

    private void RemoveConsentReference(string reference)
    {
        hl7B2BModel.ConsentReference.Remove(reference);
    }

    private async Task HandleInclude()
    {
        await form.Validate();
        if (form.IsValid)
        {
            await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2B, hl7B2BModel, true);
            await OnUpdateEditor.InvokeAsync();
        }
    }

    private async Task HandleRemove()
    {
        await UpdateAppState(UdapConstants.UdapAuthorizationExtensions.Hl7B2B, hl7B2BModel, false);
        await OnUpdateEditor.InvokeAsync();
    }

    private Task UpdateAppState(string key, HL7B2BAuthorizationExtension model, bool use)
    {
        var jsonString = JsonSerializer.Serialize(model, _jsonSerializerOptions);

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

    private async Task GoToFhirSecurityIG()
    {
        await JSRuntime.InvokeVoidAsync("open", "https://hl7.org/fhir/us/udap-security/b2b.html#b2b-authorization-extension-object", "_blank");
    }
}