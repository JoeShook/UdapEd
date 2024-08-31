using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;

public partial class Hl7B2bForm
{
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Parameter] public EventCallback<Dictionary<string, B2BAuthorizationExtension>> OnInclude { get; set; }
    [Parameter] public EventCallback<Dictionary<string, B2BAuthorizationExtension>> OnRemove { get; set; }
    
    private MudForm form;
    private B2BAuthorizationExtension hl7B2BModel = new B2BAuthorizationExtension();
    private string selectedPurposeOfUse;
    private string newPurposeOfUse;
    private string selectedConsentPolicy;
    private string selectedConsentReference;
    private string newConsentPolicy;
    private string newConsentReference;
    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    protected override async Task OnInitializedAsync()
    {
        var authExtObj = AppState.AuthorizationExtObjects.SingleOrDefault(a => a.Key == "hl7-b2b");

        if (authExtObj.Key != null && authExtObj.Value != null && !string.IsNullOrEmpty(authExtObj.Value.Json))
        {
            hl7B2BModel = JsonSerializer.Deserialize<B2BAuthorizationExtension>(authExtObj.Value.Json) ??
                          new B2BAuthorizationExtension();
            Console.WriteLine("hl7B2BModel: " + JsonSerializer.Serialize(hl7B2BModel));
        }
        else
        {
            // default starter template
            hl7B2BModel = JsonSerializer.Deserialize<B2BAuthorizationExtension>(
                "{\"version\":\"1\",\"subject_id\":\"urn:oid:2.16.840.1.113883.4.6#1234567890\",\"organization_id\":\"https://fhirlabs.net/fhir/r4\",\"organization_name\":\"FhirLabs\",\"purpose_of_use\":[\"urn:oid:2.16.840.1.113883.5.8#TREAT\"]}");
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
        hl7B2BModel.ConsentPolicy.Remove(policy);
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
            Console.WriteLine(JsonSerializer.Serialize(hl7B2BModel, _jsonSerializerOptions));
            var b2bAuthExtensions = UpdateAppState("hl7-b2b", hl7B2BModel, true);
            await OnInclude.InvokeAsync(b2bAuthExtensions);
        }
    }

    private async Task HandleRemove()
    {
        var b2bAuthExtensions = UpdateAppState("hl7-b2b", hl7B2BModel, false);
        await OnRemove.InvokeAsync(b2bAuthExtensions);
    }

    private Dictionary<string, B2BAuthorizationExtension> UpdateAppState(string key, B2BAuthorizationExtension model, bool use)
    {
        var jsonString = JsonSerializer.Serialize(model, _jsonSerializerOptions);

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

        AppState.SetProperty(this, nameof(AppState.AuthorizationExtObjects), AppState.AuthorizationExtObjects);

        return AppState.AuthorizationExtObjects
            .Where(a => a.Value.Use)
            .ToDictionary(
                a => a.Key,
                a => JsonSerializer.Deserialize<B2BAuthorizationExtension>(a.Value.Json, _jsonSerializerOptions)
            );

    }
}