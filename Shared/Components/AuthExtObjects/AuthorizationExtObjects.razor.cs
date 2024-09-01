#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;

public partial class AuthorizationExtObjects
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;
    private MudTabs _tabs;
    private StandaloneCodeEditor? _editor = null;
    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new B2BAuthorizationExtensionConverter() }
    };
    private bool _isEditorInitialized;
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isEditorInitialized && AppState.AuthorizationExtObjects.Any())
        {
            await SetEditorValue();
        }
    }

    private async Task EditorOnDidInit()
    {
        await JsRuntime.InvokeVoidAsync("setMonacoEditorResize", _editor.Id);
        _isEditorInitialized = true;
    }

    private async Task SetEditorValue()
    {
        var b2bAuthExtensions = new Dictionary<string, B2BAuthorizationExtension>();
        
        foreach (var keyValuePair in AppState.AuthorizationExtObjects.Where(a => a.Value.Use))
        {
            if (!string.IsNullOrEmpty(keyValuePair.Value.Json))
            {
                var b2bAuthExtension = JsonSerializer.Deserialize<B2BAuthorizationExtension>(keyValuePair.Value.Json, _jsonSerializerOptions);
                if (b2bAuthExtension != null)
                {
                    b2bAuthExtensions[keyValuePair.Key] = b2bAuthExtension;
                }
            }
        }

        var jsonExtObjects = JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions);
        if (_editor != null)
        {
            await InvokeAsync(() => _editor.SetValue(jsonExtObjects));
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

    private void HandleInclude(Dictionary<string, B2BAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(b2bAuthExtensions, _jsonSerializerOptions));
    }

    private void HandleRemove(Dictionary<string, B2BAuthorizationExtension> b2bAuthExtensions)
    {
        _editor?.SetValue(JsonSerializer.Serialize(AppState.AuthorizationExtObjects, _jsonSerializerOptions));
    }
}
