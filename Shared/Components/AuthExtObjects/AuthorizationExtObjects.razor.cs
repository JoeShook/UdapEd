#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System;
using System.Text.Json;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.Platform;
using MudBlazor;
using Udap.Model;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;

public partial class AuthorizationExtObjects
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;
    private StandaloneCodeEditor _editor = null!;
    private bool _isEditorInitialized;
    private Hl7B2bUserForm? _hl7B2BUserForm;
    private Hl7B2bForm? _hl7B2BForm;
    private TefcaIasForm? _tefcaIasForm;

    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isEditorInitialized && AppState.AuthorizationExtObjects.Any())
        {
            await SetEditorValue();
        }
    }

    private StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "json"
        };
    }

    private async Task EditorOnDidInit()
    {
        await _editor.Layout(new Dimension() { Height = 600, Width = 200 });
        await JsRuntime.InvokeVoidAsync("setMonacoEditorResize", _editor.Id);
        _isEditorInitialized = true;
    }

    private async Task SetEditorValue()
    {
        var b2bAuthExtensions = new Dictionary<string, object>();

        foreach (var keyValuePair in AppState.AuthorizationExtObjects.Where(a => a.Value.Use))
        {
            if (!string.IsNullOrEmpty(keyValuePair.Value.Json))
            {
                if (keyValuePair.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2B)
                {
                    var b2bAuthExtension = JsonSerializer.Deserialize<HL7B2BAuthorizationExtension>(keyValuePair.Value.Json,
                            _jsonSerializerOptions);
                    if (b2bAuthExtension != null)
                    {
                        b2bAuthExtensions[keyValuePair.Key] = b2bAuthExtension;
                    }
                }
                if (keyValuePair.Key == UdapConstants.UdapAuthorizationExtensions.Hl7B2BUSER)
                {
                    var b2bAuthExtension = JsonSerializer.Deserialize<HL7B2BUserAuthorizationExtension>(keyValuePair.Value.Json,
                        _jsonSerializerOptions);
                    if (b2bAuthExtension != null)
                    {
                        b2bAuthExtensions[keyValuePair.Key] = b2bAuthExtension;
                    }
                }
                if (keyValuePair.Key == UdapConstants.UdapAuthorizationExtensions.TEFCAIAS)
                {
                    var b2bAuthExtension = JsonSerializer.Deserialize<TEFCAIASAuthorizationExtension>(keyValuePair.Value.Json,
                        _jsonSerializerOptions);
                    if (b2bAuthExtension != null)
                    {
                        b2bAuthExtensions[keyValuePair.Key] = b2bAuthExtension;
                    }
                }
            }
        }

        var jsonExtObjects = JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions);
        await InvokeAsync(() => _editor.SetValue(jsonExtObjects));
    }
    

    private async Task SaveChanges()
    {
        try
        {
            var jsonElement = JsonDocument.Parse(await _editor.GetValue()).RootElement;
            var extensions = Udap.Model.PayloadSerializer.Deserialize(jsonElement);

            // Iterate through each extension and save the data
            foreach (var extension in extensions)
            {
                if (extension.Value is HL7B2BAuthorizationExtension b2bAuthExtensions)
                {
                    var matchingItem = AppState.AuthorizationExtObjects.FirstOrDefault(a => a.Key == extension.Key);
                    if (matchingItem.Key != null)
                    {
                        matchingItem.Value.Json = b2bAuthExtensions.SerializeToJson(true);
                    }
                }
                else if (extension.Value is HL7B2BUserAuthorizationExtension b2bUserAuthExtensions)
                {
                    var matchingItem = AppState.AuthorizationExtObjects.FirstOrDefault(a => a.Key == extension.Key);
                    if (matchingItem.Key != null)
                    {
                        matchingItem.Value.Json = b2bUserAuthExtensions.SerializeToJson(true);
                    }
                }
                else if (extension.Value is TEFCAIASAuthorizationExtension tefcAuthExtensions)
                {
                    var matchingItem = AppState.AuthorizationExtObjects.FirstOrDefault(a => a.Key == extension.Key);
                    if (matchingItem.Key != null)
                    {
                        matchingItem.Value.Json = tefcAuthExtensions.SerializeToJson(true);
                    }
                }
            }

            await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationExtObjects),
                AppState.AuthorizationExtObjects);
            
            _hl7B2BUserForm?.Update();
            _hl7B2BForm?.Update();
            _tefcaIasForm?.Update();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
            return; 
        }
    }

    public class Marker
    {
        public string? Message { get; set; }
        public int Severity { get; set; }
    }

    private void HandleInclude(Dictionary<string, HL7B2BAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions));
    }

    private void HandleRemove(Dictionary<string, HL7B2BAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(AppState.AuthorizationExtObjects, _jsonSerializerOptions));
    }

    private void HandleInclude(Dictionary<string, HL7B2BUserAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions));
    }

    private void HandleRemove(Dictionary<string, HL7B2BUserAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(AppState.AuthorizationExtObjects, _jsonSerializerOptions));
    }

    private void HandleInclude(Dictionary<string, TEFCAIASAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions));
    }

    private void HandleRemove(Dictionary<string, TEFCAIASAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(AppState.AuthorizationExtObjects, _jsonSerializerOptions));
    }
}
