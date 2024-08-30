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
using MudBlazor;
using Udap.Model.UdapAuthenticationExtensions;
using UdapEd.Shared.Model.AuthExtObjects;

namespace UdapEd.Shared.Components.AuthExtObjects;

public partial class AuthorizationExtObjects
{
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;
    private StandaloneCodeEditor? _editor = null;
    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };
    private int _isEditorInitialized = 0;
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }
    
    protected override void OnAfterRender(bool firstRender)
    {
        // Hacky, but can't figure out the lifecylce issue yet.
        // Specifically if you reload the browser from a different page and 
        // then navigate here.
        if (_isEditorInitialized < 2 && AppState.AuthorizationExtObjects.Any())
        {
            SetEditorValue();
        }
    }

    private void SetEditorValue()
    {
        var b2bAuthExtensions = new Dictionary<string, B2BAuthorizationExtension>();
        Console.WriteLine(AppState.AuthorizationExtObjects.Count);
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
        
        _editor?.SetValue(jsonExtObjects);
        _isEditorInitialized++;
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
